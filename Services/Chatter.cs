using System;
using System.Collections.Generic;

namespace EmpireGame.Services
{
    /// <summary>
    /// Military-style radio chatter for dramatic game events.
    /// </summary>
    public static class Chatter
    {
        private static readonly Random _rng = new Random();

        private static string Pick(IReadOnlyList<string> lines) =>
            lines[_rng.Next(lines.Count)];

        // ── Bombing Run ──────────────────────────────────────────────────────────

        public static string BombingRunQueued(string target) => Pick(new[]
        {
            $"EAGLE FLIGHT, you are go for bombing run. Target: {target}. Godspeed.",
            $"Fox Two, Fox Two — bomb run authorized. Target locked at {target}.",
            $"ALPHA STRIKE inbound. All units clear the area around {target}.",
            $"Mission hot. Weapons free on {target}. Do not abort.",
            $"BULLSEYE confirmed at {target}. You're cleared in hot, Eagle.",
            $"Command copies. Strike package en route to {target}. Keep it tight.",
            $"Ordnance armed. Beginning attack profile on {target}.",
        });

        public static string BombingRunCancelled() => Pick(new[]
        {
            "EAGLE FLIGHT, abort! Abort! RTB immediately.",
            "Ceasefire, ceasefire. Strike package recalled.",
            "Mission scrubbed. All birds return to base.",
            "Negative on the strike — target aborted. Come home.",
            "ALPHA STRIKE recalled. Bring the escorts back.",
            "Weapons safe. Mission cancelled. Return to base.",
        });

        public static string BombingRunOnTarget(string target) => Pick(new[]
        {
            $"BOMBS AWAY! Splash on {target}!",
            $"Direct hit confirmed at {target}. Good effect on target.",
            $"EAGLE FLIGHT reports bombs released. Smoke on {target}.",
            $"Target {target} is hit. BDA pending.",
            $"Splash! Splash! Ordnance on deck at {target}.",
            $"STRIKE complete. Target at {target} engaged.",
            $"Fox One away — target {target} is burning.",
        });

        public static string BombingRunRTB() => Pick(new[]
        {
            "Strike complete. Eagle Flight RTB.",
            "Good hits. Come on home, boys.",
            "ALPHA STRIKE returning to base. Well done.",
            "All birds RTB. Outstanding work today.",
            "Mission complete. Fuel state?  RTB now.",
        });

        // ── Satellite ────────────────────────────────────────────────────────────

        public static string SatelliteLaunched(string type) => Pick(new[]
        {
            $"T-minus zero — {type} is away. Godspeed.",
            $"Stage separation confirmed. {type} on nominal trajectory.",
            $"ORBITAL INSERTION successful. {type} is live.",
            $"Rocket nominal. {type} achieved orbit. Telemetry green.",
            $"Launch successful. {type} is on station.",
            $"Houston copies — {type} is in the black.",
        });

        public static string SatelliteVisionUpdate() => Pick(new[]
        {
            "Downlink established. Imagery coming through.",
            "Bird's eye view online. We can see everything.",
            "Satellite telemetry nominal. Feeds are live.",
            "High-value intel inbound from orbit.",
        });

        // ── ASAT / Kill ──────────────────────────────────────────────────────────

        public static string AsatEngaging() => Pick(new[]
        {
            "ASAT lock confirmed. Interceptor away!",
            "Hostile satellite in range — hunter-killer engaged.",
            "Kinetic intercept authorized. Fox Three!",
            "ASAT targeting solution locked. Firing.",
            "Enemy bird acquired. Interceptor launched.",
        });

        public static string AsatKill() => Pick(new[]
        {
            "SPLASH — hostile satellite destroyed!",
            "Impact confirmed. Enemy bird is gone. We're blind up there too.",
            "Kinetic kill confirmed. Both birds down.",
            "Target destroyed. Debris field spreading.",
            "HUNTER-KILLER successful. Enemy orbital asset neutralized.",
        });

        // ── Combat ───────────────────────────────────────────────────────────────

        public static string UnitDestroyed(string unitName) => Pick(new[]
        {
            $"{unitName} is down! All hands, we've lost {unitName}.",
            $"Command — we've lost the {unitName}. Requesting support.",
            $"{unitName} destroyed. Pull back, pull back.",
            $"Contact destroyed — that was our {unitName}.",
            $"MAYDAY — {unitName} hit and down.",
        });

        public static string EnemyUnitDestroyed(string unitName) => Pick(new[]
        {
            $"Splash one {unitName}! Good kill, good kill.",
            $"Enemy {unitName} neutralized. Area clear.",
            $"Target down — {unitName} destroyed.",
            $"Confirmed kill on enemy {unitName}. Push forward.",
            $"That {unitName} won't bother us again.",
        });

        public static string StructureDestroyed(string structName) => Pick(new[]
        {
            $"ENEMY {structName} is down! We own that ground now.",
            $"Structure destroyed. {structName} is gone.",
            $"Good effect — {structName} neutralized.",
            $"{structName} eliminated. Advance!",
        });

        // ── Production ───────────────────────────────────────────────────────────

        public static string UnitProduced(string unitName) => Pick(new[]
        {
            $"{unitName} ready for deployment. All systems nominal.",
            $"New {unitName} off the line. Standing by for orders.",
            $"{unitName} is operational. Where do you need us?",
            $"Production complete. {unitName} reporting for duty.",
            $"{unitName} armed and ready. Awaiting assignment.",
        });

        // ── Escort ───────────────────────────────────────────────────────────────

        public static string EscortJoining(int count) => Pick(new[]
        {
            $"{count} fighter{(count != 1 ? "s" : "")} joining the package. We've got you covered.",
            $"Escort flight of {count} on your wing. Let's get it done.",
            $"GUARDIAN flight, {count} ship{(count != 1 ? "s" : "")}, in position.",
            $"Fighter cover assigned. {count} aboard the package.",
        });

        public static string EscortShielding(string escortName) => Pick(new[]
        {
            $"{escortName} intercepts incoming fire! Taking hits for the bomber!",
            $"GUARDIAN taking fire — covering the strike package!",
            $"{escortName} in the way! Absorbing the hit!",
            $"Escort engaged! {escortName} screens the bomber!",
        });

        public static string EscortDown(string escortName) => Pick(new[]
        {
            $"{escortName} is down! Continuing the run!",
            $"We lost {escortName}! Bomber is exposed!",
            $"GUARDIAN is gone. Bomber, you're on your own!",
            $"{escortName} destroyed. Protect that bomber!",
        });
    }
}
