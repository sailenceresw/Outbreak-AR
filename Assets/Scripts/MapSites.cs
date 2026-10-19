using System;
using System.Collections.Generic;
using UnityEngine;

public enum SiteKind { Outbreak, Cache, Nest }

public class MapSite
{
    public string id;
    public SiteKind kind;
    public double lat;
    public double lon;
    public string name;
    // caches at real parks: better loot
    public bool rare;
}

// what a map site fight looks like and what it pays out
public class SiteEncounter
{
    public string siteId;
    public LevelDefinition level;
    public float mutantChance;
    public int rewardSupply;
    public int rewardXP;
    public int rewardTraps;
    public bool nest;
}

// Outbreak sites, supply caches and boss nests around the player. Sites are generated from a grid
// over the real world, seeded by cell (and by day for fights), so everyone standing in the same place
// on the same day finds the same sites and they don't move while walking. Saved progress is per device.
public static class MapSites
{
    // about 220 m per cell
    const double CellDegrees = 0.002;
    // walk this close to use a site
    public const double UseRadiusMeters = 45.0;
    const int CacheCooldownMinutes = 30;

    static readonly string[] OutbreakNames = { "Infected Block", "Quarantine Breach", "Overrun Street", "Dead Zone", "Outbreak Cluster" };
    static readonly string[] NestNames = { "Brute Nest", "Hive of the Dead", "Abomination Lair" };
    static readonly string[] CacheNames = { "Supply Cache", "Abandoned Stash", "Army Crate", "Medic Drop" };

    public static List<MapSite> Around(double lat, double lon, double radiusMeters, List<MapSite> parkCaches)
    {
        List<MapSite> sites = new List<MapSite>();
        int day = int.Parse(PlayerProfile.DateKey(DateTime.Now));
        long centerX = (long)Math.Floor(lon / CellDegrees);
        long centerY = (long)Math.Floor(lat / CellDegrees);
        int reach = (int)Math.Ceiling(radiusMeters / 200.0) + 1;

        for (long y = centerY - reach; y <= centerY + reach; y++)
        {
            for (long x = centerX - reach; x <= centerX + reach; x++)
            {
                // fights rotate every day; caches stay put
                System.Random daily = new System.Random(Hash(x, y, day));
                System.Random fixedSeed = new System.Random(Hash(x, y, 0));

                double roll = daily.NextDouble();
                if (roll < 0.42) Add(sites, SiteKind.Outbreak, x, y, daily, OutbreakNames, "o", day, lat, lon, radiusMeters);
                else if (roll < 0.47 && PlayerProfile.Level >= 3) Add(sites, SiteKind.Nest, x, y, daily, NestNames, "n", day, lat, lon, radiusMeters);

                if (fixedSeed.NextDouble() < 0.3) Add(sites, SiteKind.Cache, x, y, fixedSeed, CacheNames, "c", 0, lat, lon, radiusMeters);
            }
        }

        if (parkCaches != null)
        {
            foreach (MapSite park in parkCaches)
            {
                if (DistanceMeters(lat, lon, park.lat, park.lon) <= radiusMeters) sites.Add(park);
            }
        }
        return sites;
    }

    static void Add(List<MapSite> sites, SiteKind kind, long x, long y, System.Random random, string[] names,
        string prefix, int day, double lat, double lon, double radiusMeters)
    {
        double siteLat = (y + 0.15 + random.NextDouble() * 0.7) * CellDegrees;
        double siteLon = (x + 0.15 + random.NextDouble() * 0.7) * CellDegrees;
        if (DistanceMeters(lat, lon, siteLat, siteLon) > radiusMeters) return;
        sites.Add(new MapSite
        {
            id = prefix + x + "_" + y + (day > 0 ? "_" + day : string.Empty),
            kind = kind,
            lat = siteLat,
            lon = siteLon,
            name = names[random.Next(names.Length)],
        });
    }

    // a rare cache placed at a real park found through OpenStreetMap
    public static MapSite ParkCache(double lat, double lon, string parkName)
    {
        return new MapSite
        {
            id = "p" + Math.Round(lat, 5) + "_" + Math.Round(lon, 5),
            kind = SiteKind.Cache,
            lat = lat,
            lon = lon,
            name = string.IsNullOrEmpty(parkName) ? "Park Cache" : parkName,
            rare = true,
        };
    }

    // ---------- State ----------

    public static bool IsCleared(MapSite site)
    {
        if (site.kind == SiteKind.Cache) return CacheReadyInMinutes(site) > 0;
        return PlayerPrefs.GetInt("Site_" + site.id, 0) == 1;
    }

    public static void MarkCleared(string siteId)
    {
        PlayerPrefs.SetInt("Site_" + siteId, 1);
        PlayerPrefs.Save();
    }

    public static int CacheReadyInMinutes(MapSite site)
    {
        long readyAt = long.Parse(PlayerPrefs.GetString("Cache_" + site.id, "0"));
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return readyAt > now ? (int)Math.Ceiling((readyAt - now) / 60.0) : 0;
    }

    // open a cache: returns a description of the loot and starts its cooldown
    public static string OpenCache(MapSite site)
    {
        long readyAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + CacheCooldownMinutes * 60;
        PlayerPrefs.SetString("Cache_" + site.id, readyAt.ToString());
        LifetimeStats.Add(Stat.CachesOpened, 1, false);

        System.Random random = new System.Random();
        int multiplier = site.rare ? 2 : 1;
        List<string> loot = new List<string>();

        int traps = random.Next(1, 4) * multiplier;
        Inventory.Add(ItemId.Trap, traps);
        loot.Add("+" + traps + " traps");

        int supply = random.Next(40, 121) * multiplier;
        PlayerProfile.AddSupply(supply);
        loot.Add("+" + supply + " SP");

        double roll = random.NextDouble();
        if (roll < 0.35 * multiplier) { Inventory.Add(ItemId.GrenadePack, 1); loot.Add("Grenade Pack"); }
        else if (roll < 0.55 * multiplier) { Inventory.Add(ItemId.AmmoCache, 1); loot.Add("Ammo Cache"); }
        if (random.NextDouble() < 0.08 * multiplier) { Inventory.Add(ItemId.Adrenaline, 1); loot.Add("Adrenaline Shot"); }

        // rare park caches can hold a gun the player doesn't own yet
        if (site.rare && random.NextDouble() < 0.12)
        {
            foreach (WeaponDefinition weapon in Arsenal.All)
            {
                if (Arsenal.Owns(weapon.id)) continue;
                Arsenal.Grant(weapon.id);
                loot.Add("<color=#FFC832>NEW GUN: " + weapon.name + "</color>");
                break;
            }
        }

        PlayerPrefs.Save();
        return string.Join("\n", loot);
    }

    // ---------- Encounters ----------

    public static SiteEncounter EncounterFor(MapSite site)
    {
        int strength = Mathf.Clamp(GameProgress.UnlockedLevel, 0, 7);
        if (site.kind == SiteKind.Nest)
        {
            return new SiteEncounter
            {
                siteId = site.id,
                nest = true,
                mutantChance = 0.15f,
                rewardSupply = 450,
                rewardXP = 350,
                rewardTraps = 3,
                level = new LevelDefinition
                {
                    chapter = "Boss nest", title = site.name,
                    story = "Something huge guards this place.\nBring it down.",
                    objective = LevelObjective.Boss, zombieCount = 6 + strength,
                    maxAlive = 4, runnerChance = 0.3f, bruteChance = 0.1f, spitterChance = 0.1f, crawlerChance = 0.1f,
                    speedMultiplier = 1f + strength * 0.03f, bonusAmmo = 20,
                },
            };
        }

        return new SiteEncounter
        {
            siteId = site.id,
            mutantChance = 0.12f,
            rewardSupply = 150,
            rewardXP = 120,
            rewardTraps = 1,
            level = new LevelDefinition
            {
                chapter = "Outbreak site", title = site.name,
                story = "The infection is spreading from here.\nClear it out.",
                objective = LevelObjective.Eliminate, zombieCount = 8 + strength,
                maxAlive = 4 + strength / 3, runnerChance = 0.3f, bruteChance = 0.08f + strength * 0.01f,
                spitterChance = 0.12f, crawlerChance = 0.12f, speedMultiplier = 1f + strength * 0.03f,
                hordeEvents = strength >= 4, bonusAmmo = 10,
            },
        };
    }

    // ---------- Geometry ----------

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadius = 6371000.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    static int Hash(long x, long y, int salt)
    {
        unchecked
        {
            long h = 1469598103934665603L;
            h = (h ^ x) * 1099511628211L;
            h = (h ^ y) * 1099511628211L;
            h = (h ^ salt) * 1099511628211L;
            return (int)(h ^ (h >> 32));
        }
    }
}
