using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

// The world map: the player's GPS position on a dark street map, with outbreak sites, supply caches
// (rare ones at real parks) and boss nests around them. Walk to a site to fight there or open a cache.
// Tiles: CARTO basemaps over OpenStreetMap data; parks: OpenStreetMap's Overpass API. No keys needed.
public class MapScreen : MonoBehaviour
{
    const int Zoom = 17;
    const int TileSize = 256;
    const int TileRadius = 2;
    const double SiteRadiusMeters = 650;
    // Esri's dark grey canvas (no key needed; zoom/row/column, up to zoom 16), OpenStreetMap as a fallback
    const string TileUrl = "https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{0}/{1}/{2}";
    const int SourceZoom = 16;
    const string FallbackTileUrl = "https://tile.openstreetmap.org/{0}/{1}/{2}.png";
    const string UserAgent = "OutbreakAR/1.0 (Unity; personal project)";

    static readonly Dictionary<string, Texture2D> tileCache = new Dictionary<string, Texture2D>();
    static List<MapSite> parkCaches;
    static bool parksRequested;

    TMP_Text template;
    Action onClosed;
    Action<SiteEncounter> onFight;
    RectTransform mapLayer;
    RectTransform siteLayer;
    RectTransform playerMarker;
    TMP_Text statusText;
    TMP_Text walkText;
    RectTransform infoPanel;
    TMP_Text infoTitle;
    TMP_Text infoBody;
    Button infoButton;
    TMP_Text infoButtonLabel;

    readonly Dictionary<string, RawImage> tileImages = new Dictionary<string, RawImage>();
    // each marker and the site it stands for
    readonly Dictionary<RectTransform, MapSite> markers = new Dictionary<RectTransform, MapSite>();
    List<MapSite> sites = new List<MapSite>();
    MapSite selected;
    bool hasFix;
    double lat, lon;
    double lastWalkLat, lastWalkLon;
    bool hasWalkPoint;
    long centerTileX = long.MinValue, centerTileY = long.MinValue;
    float nextSiteRefresh;

    public static MapScreen Show(Canvas canvas, TMP_Text template, Action<SiteEncounter> onFight, Action onClosed)
    {
        GameObject panel = new GameObject("MapScreen", typeof(RectTransform));
        RectTransform root = panel.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root, Vector2.zero, Vector2.one);
        root.SetAsLastSibling();

        Image background = panel.AddComponent<Image>();
        background.sprite = GameFX.WhiteSprite;
        background.color = new Color(0.05f, 0.06f, 0.07f, 1f);

        MapScreen map = panel.AddComponent<MapScreen>();
        map.template = template;
        map.onFight = onFight;
        map.onClosed = onClosed;
        map.Build(root);
        map.StartCoroutine(map.StartLocation());
        return map;
    }

    // ---------- Layout ----------

    void Build(RectTransform root)
    {
        // the map itself, clipped to the screen; tiles and markers move together inside it
        RectTransform viewport = NewRect(root, "Viewport");
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();

        mapLayer = NewRect(viewport, "Tiles");
        mapLayer.anchorMin = mapLayer.anchorMax = new Vector2(0.5f, 0.5f);
        siteLayer = NewRect(viewport, "Sites");
        siteLayer.anchorMin = siteLayer.anchorMax = new Vector2(0.5f, 0.5f);

        // the player: a blue dot with a soft ring, always in the centre
        playerMarker = NewRect(viewport, "Player");
        playerMarker.anchorMin = playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarker.sizeDelta = new Vector2(90f, 90f);
        RawImage ring = playerMarker.gameObject.AddComponent<RawImage>();
        ring.texture = GameFX.RingSprite.texture;
        ring.color = new Color(0.35f, 0.75f, 1f, 0.8f);
        RectTransform dot = NewRect(playerMarker, "Dot");
        dot.sizeDelta = new Vector2(26f, 26f);
        Image dotImage = dot.gameObject.AddComponent<Image>();
        dotImage.sprite = UIStyle.Rounded;
        dotImage.type = Image.Type.Sliced;
        dotImage.pixelsPerUnitMultiplier = 2f;
        dotImage.color = new Color(0.35f, 0.75f, 1f);

        // top bar
        RectTransform top = NewRect(root, "TopBar");
        Stretch(top, new Vector2(0f, 0.86f), new Vector2(1f, 1f));
        Image topShade = top.gameObject.AddComponent<Image>();
        topShade.sprite = GameFX.WhiteSprite;
        topShade.color = new Color(0f, 0f, 0f, 0.7f);
        TMP_Text title = NewText(top, "WORLD MAP", 34f, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(1f, 0.95f));
        title.color = UIStyle.Toxic;
        title.characterSpacing = 8f;
        statusText = NewText(top, "Finding your location...", 18f, FontStyles.Normal, new Vector2(0f, 0.25f), new Vector2(1f, 0.5f));
        walkText = NewText(top, string.Empty, 17f, FontStyles.Normal, new Vector2(0f, 0.02f), new Vector2(1f, 0.25f));
        walkText.color = new Color(0.6f, 0.85f, 1f);

        // legend and attribution along the bottom
        RectTransform bottom = NewRect(root, "Bottom");
        Stretch(bottom, new Vector2(0f, 0f), new Vector2(1f, 0.13f));
        Image bottomShade = bottom.gameObject.AddComponent<Image>();
        bottomShade.sprite = GameFX.WhiteSprite;
        bottomShade.color = new Color(0f, 0f, 0f, 0.7f);
        NewText(bottom, "<color=#FF5040>OUTBREAK</color>   <color=#FFC832>CACHE</color>   <color=#7CFF5A>PARK CACHE</color>   <color=#C080FF>NEST</color>",
            17f, FontStyles.Bold, new Vector2(0f, 0.62f), new Vector2(1f, 0.95f));
        TMP_Text credit = NewText(bottom, "Basemap © Esri, HERE, Garmin  ·  © OpenStreetMap contributors", 13f, FontStyles.Normal,
            new Vector2(0f, 0.02f), new Vector2(0.62f, 0.3f));
        credit.color = new Color(1f, 1f, 1f, 0.5f);
        Button close = UIStyle.NewButton(bottom, template, "CLOSE", UIStyle.Blood, 22f, Close);
        UIStyle.AddIcon(close, "Back");
        Stretch((RectTransform)close.transform, new Vector2(0.64f, 0.08f), new Vector2(0.96f, 0.55f));

        BuildInfoPanel(root);
    }

    void BuildInfoPanel(RectTransform root)
    {
        infoPanel = NewRect(root, "SiteInfo");
        Stretch(infoPanel, new Vector2(0.06f, 0.15f), new Vector2(0.94f, 0.36f));
        UIStyle.AddPanel(infoPanel.gameObject, UIStyle.Panel);
        infoTitle = NewText(infoPanel, string.Empty, 26f, FontStyles.Bold, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.95f));
        infoBody = NewText(infoPanel, string.Empty, 18f, FontStyles.Normal, new Vector2(0.04f, 0.38f), new Vector2(0.96f, 0.72f));
        infoButton = UIStyle.NewButton(infoPanel, template, "GO", UIStyle.Toxic, 22f, UseSelected);
        Stretch((RectTransform)infoButton.transform, new Vector2(0.2f, 0.07f), new Vector2(0.8f, 0.34f));
        infoButtonLabel = infoButton.GetComponentInChildren<TMP_Text>();
        infoPanel.gameObject.SetActive(false);
    }

    // ---------- Location ----------

    IEnumerator StartLocation()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
            // give the system dialog time to be answered
            for (float t = 0f; t < 15f; t += 0.5f)
            {
                if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation)) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
#endif
        if (!Input.location.isEnabledByUser)
        {
            statusText.text = "<color=#FF6050>Location is off.</color> Turn on location in your phone's settings.";
            yield break;
        }

        if (Input.location.status != LocationServiceStatus.Running) Input.location.Start(5f, 3f);
        for (float t = 0f; t < 25f && Input.location.status == LocationServiceStatus.Initializing; t += 0.5f)
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }
        if (Input.location.status != LocationServiceStatus.Running)
        {
            statusText.text = "<color=#FF6050>No GPS signal.</color> Try near a window or outside.";
        }
    }

    void Update()
    {
        if (PauseMenu.BackPressed()) Close();
        if (Input.location.status != LocationServiceStatus.Running) return;

        LocationInfo data = Input.location.lastData;
        if (data.timestamp <= 0) return;

        bool first = !hasFix;
        hasFix = true;
        lat = data.latitude;
        lon = data.longitude;
        statusText.text = "GPS accuracy " + Mathf.RoundToInt(data.horizontalAccuracy) + " m   ·   " + sites.Count + " sites nearby";

        TrackWalking(data);
        UpdateTiles();
        PositionLayers();

        if (first || Time.unscaledTime >= nextSiteRefresh)
        {
            nextSiteRefresh = Time.unscaledTime + 10f;
            if (!parksRequested) StartCoroutine(FetchParks(lat, lon));
            RefreshSites();
        }
        else
        {
            PositionMarkers();
        }
        if (selected != null) ShowInfo(selected);
    }

    // ---------- Tiles ----------

    void UpdateTiles()
    {
        LatLonToPixel(lat, lon, out double px, out double py);
        long tileX = (long)Math.Floor(px / TileSize);
        long tileY = (long)Math.Floor(py / TileSize);
        if (tileX == centerTileX && tileY == centerTileY) return;
        centerTileX = tileX;
        centerTileY = tileY;

        HashSet<string> wanted = new HashSet<string>();
        for (long y = tileY - TileRadius; y <= tileY + TileRadius; y++)
        {
            for (long x = tileX - TileRadius; x <= tileX + TileRadius; x++)
            {
                string key = x + "_" + y;
                wanted.Add(key);
                if (tileImages.ContainsKey(key)) continue;

                RectTransform tile = NewRect(mapLayer, "Tile " + key);
                tile.anchorMin = tile.anchorMax = new Vector2(0f, 1f);
                tile.pivot = new Vector2(0f, 1f);
                tile.sizeDelta = new Vector2(TileSize, TileSize);
                tile.anchoredPosition = new Vector2(x * TileSize, -y * TileSize);
                RawImage image = tile.gameObject.AddComponent<RawImage>();
                image.color = new Color(0.12f, 0.13f, 0.15f);
                image.raycastTarget = false;
                tileImages[key] = image;
                StartCoroutine(LoadTile(x, y, key, image));
            }
        }

        // drop tiles that have scrolled far away
        List<string> stale = new List<string>();
        foreach (string key in tileImages.Keys) if (!wanted.Contains(key)) stale.Add(key);
        foreach (string key in stale)
        {
            Destroy(tileImages[key].gameObject);
            tileImages.Remove(key);
        }
    }

    IEnumerator LoadTile(long x, long y, string key, RawImage image)
    {
        // a fallback OpenStreetMap tile already downloaded for this spot
        if (tileCache.TryGetValue(key, out Texture2D cached))
        {
            image.texture = cached;
            image.color = new Color(0.42f, 0.43f, 0.47f);
            yield break;
        }

        // the dark basemap stops at zoom 16: each zoom-17 tile shows its quarter of the zoom-16 tile
        long sourceX = x / 2, sourceY = y / 2;
        string sourceKey = "esri_" + sourceX + "_" + sourceY;
        Rect quarter = new Rect((x % 2) * 0.5f, (y % 2) == 0 ? 0.5f : 0f, 0.5f, 0.5f);
        if (!tileCache.TryGetValue(sourceKey, out Texture2D source))
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(string.Format(TileUrl, SourceZoom, sourceY, sourceX)))
            {
                request.SetRequestHeader("User-Agent", UserAgent);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    source = DownloadHandlerTexture.GetContent(request);
                    source.wrapMode = TextureWrapMode.Clamp;
                    tileCache[sourceKey] = source;
                }
            }
        }
        if (image == null) yield break;
        if (source != null)
        {
            image.texture = source;
            image.uvRect = quarter;
            image.color = Color.white;
            yield break;
        }

        // fallback: the standard OpenStreetMap tile, dimmed to sit with the dark theme
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(string.Format(FallbackTileUrl, Zoom, x, y)))
        {
            request.SetRequestHeader("User-Agent", UserAgent);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success || image == null) yield break;
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            texture.wrapMode = TextureWrapMode.Clamp;
            tileCache[key] = texture;
            image.texture = texture;
            image.color = new Color(0.42f, 0.43f, 0.47f);
        }
    }

    // tiles are placed in world pixels; shift the layers so the player sits at the centre
    void PositionLayers()
    {
        LatLonToPixel(lat, lon, out double px, out double py);
        Vector2 offset = new Vector2((float)-px, (float)py);
        mapLayer.anchoredPosition = offset;
        siteLayer.anchoredPosition = Vector2.zero;
        playerMarker.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f));
    }

    static void LatLonToPixel(double latitude, double longitude, out double x, out double y)
    {
        double scale = TileSize * Math.Pow(2, Zoom);
        x = (longitude + 180.0) / 360.0 * scale;
        double sin = Math.Sin(latitude * Math.PI / 180.0);
        y = (0.5 - Math.Log((1 + sin) / (1 - sin)) / (4 * Math.PI)) * scale;
    }

    // ---------- Sites ----------

    void RefreshSites()
    {
        sites = MapSites.Around(lat, lon, SiteRadiusMeters, parkCaches);
        foreach (RectTransform marker in markers.Keys) if (marker != null) Destroy(marker.gameObject);
        markers.Clear();

        foreach (MapSite site in sites)
        {
            MapSite captured = site;
            Color color = MarkerColor(site);
            bool cleared = MapSites.IsCleared(site);

            RectTransform marker = NewRect(siteLayer, site.name);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = site.kind == SiteKind.Nest ? new Vector2(76f, 76f) : new Vector2(58f, 58f);
            Image image = marker.gameObject.AddComponent<Image>();
            image.sprite = UIStyle.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.4f;
            image.color = cleared ? new Color(color.r, color.g, color.b, 0.3f) : color;
            Button button = marker.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Select(captured));

            // a dark icon on the coloured pin: biohazard, skull, chest, or open chest for a rare park cache
            string iconName = site.kind == SiteKind.Outbreak ? "Outbreak" : site.kind == SiteKind.Nest ? "Nest" : site.rare ? "RareCache" : "Cache";
            Sprite icon = UIStyle.Icon(iconName);
            if (icon != null)
            {
                RectTransform iconRect = NewRect(marker, "Icon");
                iconRect.anchorMin = new Vector2(0.14f, 0.14f);
                iconRect.anchorMax = new Vector2(0.86f, 0.86f);
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                Image iconImage = iconRect.gameObject.AddComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                iconImage.color = new Color(0.05f, 0.05f, 0.05f, cleared ? 0.4f : 1f);
            }
            else
            {
                string glyph = site.kind == SiteKind.Outbreak ? "!" : site.kind == SiteKind.Nest ? "B" : site.rare ? "*" : "+";
                TMP_Text label = NewText(marker, cleared ? "-" : glyph, 30f, FontStyles.Bold, Vector2.zero, Vector2.one);
                label.color = new Color(0.05f, 0.05f, 0.05f);
            }
            markers[marker] = site;
        }
        PositionMarkers();
    }

    void PositionMarkers()
    {
        LatLonToPixel(lat, lon, out double px, out double py);
        foreach (KeyValuePair<RectTransform, MapSite> entry in markers)
        {
            if (entry.Key == null) continue;
            LatLonToPixel(entry.Value.lat, entry.Value.lon, out double sx, out double sy);
            entry.Key.anchoredPosition = new Vector2((float)(sx - px), (float)(py - sy));
        }
    }

    static Color MarkerColor(MapSite site)
    {
        switch (site.kind)
        {
            case SiteKind.Outbreak: return new Color(1f, 0.32f, 0.25f);
            case SiteKind.Nest: return new Color(0.75f, 0.5f, 1f);
            default: return site.rare ? new Color(0.48f, 1f, 0.35f) : new Color(1f, 0.78f, 0.2f);
        }
    }

    void Select(MapSite site)
    {
        selected = site;
        GameAudio.Play(GameAudio.Pickup, 0.4f, 1.4f);
        ShowInfo(site);
    }

    void ShowInfo(MapSite site)
    {
        infoPanel.gameObject.SetActive(true);
        double distance = MapSites.DistanceMeters(lat, lon, site.lat, site.lon);
        bool inRange = distance <= MapSites.UseRadiusMeters;
        bool cleared = MapSites.IsCleared(site);

        string kind = site.kind == SiteKind.Outbreak ? "Outbreak site - clear it for SP, XP and a trap"
            : site.kind == SiteKind.Nest ? "Boss nest - big rewards, tougher fight"
            : site.rare ? "Rare park cache - double loot, a chance of a new gun"
            : "Supply cache - traps, SP and items";
        infoTitle.text = site.name.ToUpper();
        infoTitle.color = MarkerColor(site);
        infoBody.text = kind + "\n<color=#AAAAAA>" + Mathf.RoundToInt((float)distance) + " m away</color>";

        if (cleared)
        {
            infoButtonLabel.text = site.kind == SiteKind.Cache ? "REFILLS IN " + MapSites.CacheReadyInMinutes(site) + " MIN" : "CLEARED TODAY";
            infoButton.interactable = false;
        }
        else if (!inRange)
        {
            infoButtonLabel.text = "WALK CLOSER (" + Mathf.RoundToInt((float)(distance - MapSites.UseRadiusMeters)) + " M)";
            infoButton.interactable = false;
        }
        else
        {
            infoButtonLabel.text = site.kind == SiteKind.Cache ? "OPEN CACHE" : "FIGHT HERE";
            infoButton.interactable = true;
        }
    }

    void UseSelected()
    {
        if (selected == null || MapSites.IsCleared(selected)) return;
        if (MapSites.DistanceMeters(lat, lon, selected.lat, selected.lon) > MapSites.UseRadiusMeters) return;

        if (selected.kind == SiteKind.Cache)
        {
            string loot = MapSites.OpenCache(selected);
            GameAudio.Play(GameAudio.Fanfare, 0.8f, 1.2f);
            infoBody.text = "<color=#FFC832>CACHE OPENED</color>\n" + loot.Replace("\n", "   ");
            RefreshSites();
            return;
        }

        GameAudio.Play(GameAudio.Impact, 0.9f);
        SiteEncounter encounter = MapSites.EncounterFor(selected);
        Destroy(gameObject);
        onFight?.Invoke(encounter);
    }

    // ---------- Walking ----------

    void TrackWalking(LocationInfo data)
    {
        string dayKey = "Walk_" + PlayerProfile.DateKey(DateTime.Now);
        float walked = PlayerPrefs.GetFloat(dayKey, 0f);

        // ignore poor fixes and GPS jumps; only real steps count
        if (data.horizontalAccuracy <= 35f)
        {
            if (!hasWalkPoint)
            {
                lastWalkLat = data.latitude;
                lastWalkLon = data.longitude;
                hasWalkPoint = true;
            }
            else
            {
                double step = MapSites.DistanceMeters(lastWalkLat, lastWalkLon, data.latitude, data.longitude);
                if (step >= 4.0 && step <= 60.0)
                {
                    float before = walked;
                    walked += (float)step;
                    PlayerPrefs.SetFloat(dayKey, walked);
                    lastWalkLat = data.latitude;
                    lastWalkLon = data.longitude;

                    // a mission tick every 100 m, a reward every kilometre
                    int ticks = Mathf.FloorToInt(walked / 100f) - Mathf.FloorToInt(before / 100f);
                    for (int i = 0; i < ticks; i++) MissionSystem.Report(MissionType.WalkDistance);
                    if (Mathf.FloorToInt(walked / 1000f) > Mathf.FloorToInt(before / 1000f))
                    {
                        Inventory.Add(ItemId.Trap, 2);
                        PlayerProfile.AddSupply(100);
                        GameAudio.Play(GameAudio.Fanfare, 0.8f);
                        statusText.text = "<color=#7CFF5A>WALKING REWARD: +2 traps, +100 SP</color>";
                    }
                }
                else if (step > 60.0)
                {
                    // a jump (GPS drift or a ride): restart from here without counting it
                    lastWalkLat = data.latitude;
                    lastWalkLon = data.longitude;
                }
            }
        }

        float nextReward = (Mathf.Floor(walked / 1000f) + 1f) * 1000f;
        walkText.text = "Walked today " + (walked / 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " km   ·   next reward at "
            + (nextReward / 1000f).ToString("0", CultureInfo.InvariantCulture) + " km";
    }

    // ---------- Parks (OpenStreetMap Overpass) ----------

    IEnumerator FetchParks(double atLat, double atLon)
    {
        parksRequested = true;
        string query = "[out:json][timeout:10];(way[\"leisure\"=\"park\"](around:900," +
            atLat.ToString(CultureInfo.InvariantCulture) + "," + atLon.ToString(CultureInfo.InvariantCulture) +
            ");relation[\"leisure\"=\"park\"](around:900," + atLat.ToString(CultureInfo.InvariantCulture) + "," +
            atLon.ToString(CultureInfo.InvariantCulture) + "););out center 8;";
        string url = "https://overpass-api.de/api/interpreter?data=" + UnityWebRequest.EscapeURL(query);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("User-Agent", UserAgent);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) yield break;

            List<MapSite> found = new List<MapSite>();
            // each element: "center": { "lat": .., "lon": .. } and optionally "name" in its tags
            string[] elements = request.downloadHandler.text.Split(new[] { "\"type\":" }, StringSplitOptions.None);
            foreach (string element in elements)
            {
                Match center = Regex.Match(element, "\"center\"\\s*:\\s*\\{\\s*\"lat\"\\s*:\\s*([-\\d\\.]+)\\s*,\\s*\"lon\"\\s*:\\s*([-\\d\\.]+)");
                if (!center.Success) continue;
                Match name = Regex.Match(element, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                found.Add(MapSites.ParkCache(
                    double.Parse(center.Groups[1].Value, CultureInfo.InvariantCulture),
                    double.Parse(center.Groups[2].Value, CultureInfo.InvariantCulture),
                    name.Success ? name.Groups[1].Value + " Cache" : null));
            }
            parkCaches = found;
            RefreshSites();
        }
    }

    // ---------- Shared ----------

    void Close()
    {
        GameAudio.Play(GameAudio.DryFire, 0.6f, 1.2f);
        Action closed = onClosed;
        Destroy(gameObject);
        closed?.Invoke();
    }

    RectTransform NewRect(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    TMP_Text NewText(RectTransform parent, string value, float size, FontStyles style, Vector2 min, Vector2 max)
    {
        TMP_Text text = Instantiate(template, parent, false);
        text.name = "Label";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
        Stretch(text.rectTransform, min, max);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
