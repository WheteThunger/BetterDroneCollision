using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Drone Collision", "WhiteThunder", "1.0.0")]
    [Description("Overhauls drone collision damage so it's more intuitive.")]
    internal class BetterDroneCollision : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin DroneSettings;

        private static BetterDroneCollision _pluginInstance;
        private static Configuration _pluginConfig;

        private const float ReplacementHurtVelocityThreshold = float.MaxValue;

        private float? _vanillaHurtVelocityThreshold;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;

            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null)
                    continue;

                OnEntitySpawned(drone);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                ResetDrone(drone);
            }

            _pluginConfig = null;
            _pluginInstance = null;
        }

        private void OnEntitySpawned(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            if (_vanillaHurtVelocityThreshold == null)
                _vanillaHurtVelocityThreshold = drone.hurtVelocityThreshold;

            // Delay to give other plugins a moment to cache the drone id so they can block this.
            NextTick(() =>
            {
                if (drone != null)
                    TryReplaceDroneCollision(drone);
            });
        }

        #endregion

        #region Helper Methods

        private static bool DroneCollisionReplaceWasBlocked(Drone drone)
        {
            object hookResult = Interface.CallHook("OnDroneCollisionReplace", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool IsDroneEligible(Drone drone) => !(drone is DeliveryDrone);

        private static bool TryReplaceDroneCollision(Drone drone)
        {
            if (DroneCollisionReplaceWasBlocked(drone))
                return false;

            drone.hurtVelocityThreshold = ReplacementHurtVelocityThreshold;
            drone.gameObject.AddComponent<DroneCollisionReplacer>();
            return true;
        }

        private void ResetDrone(Drone drone)
        {
            if (drone.hurtVelocityThreshold == ReplacementHurtVelocityThreshold
                && _vanillaHurtVelocityThreshold != null)
            {
                drone.hurtVelocityThreshold = (float)_vanillaHurtVelocityThreshold;
            }

            var component = drone.GetComponent<DroneCollisionReplacer>();
            if (component == null)
                return;

            UnityEngine.Object.Destroy(component);
        }

        #endregion

        #region Collision Replacer

        private class DroneCollisionReplacer : EntityComponent<Drone>
        {
            private float _nextDamageTime;

	        private void OnCollisionEnter(Collision collision)
            {
                if (collision == null || collision.gameObject == null)
                    return;

                if (Time.time < _nextDamageTime)
                    return;

                var magnitude = collision.relativeVelocity.magnitude;
                if (magnitude < _pluginConfig.MinCollisionVelocity)
                    return;

                // Avoid damage when landing.
                if (Vector3.Dot(collision.relativeVelocity.normalized, baseEntity.transform.up) > 0.5f)
                    return;

                var damage = magnitude * _pluginConfig.CollisionDamageMultiplier;

                // If DroneSettings is not loaded, it's probably safe to assume that drones are using default protection properties.
                // Default protection properties make a drone immune to collision damage, so bypass protection.
                // Without this bypass, using this plugin standalone would make drones immune to collision which is not desirable.
                var useProtection = _pluginInstance.DroneSettings != null;
                baseEntity.Hurt(damage, DamageType.Collision, useProtection: useProtection);

                Interface.CallHook("OnDroneCollisionImpact", baseEntity, collision);
                _nextDamageTime = Time.time + _pluginConfig.MinTimeBetweenImpacts;
            }
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("MinCollisionVelocity")]
            public float MinCollisionVelocity = 3;

            [JsonProperty("MinTimeBetweenImpacts")]
            public float MinTimeBetweenImpacts = 0.25f;

            [JsonProperty("CollisionDamageMultiplier")]
            public float CollisionDamageMultiplier = 1;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion
    }
}
