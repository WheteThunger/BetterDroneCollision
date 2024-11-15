## Features

- Overhauls RC drone collision damage so it's more intuitive
- Avoids collision damage when landing the drone
- Avoids collision damage with dropped items and timed explosives
- Prevents drone automatic descent when colliding with dropped items

## Configuration

Default configuration:

```json
{
  "MinCollisionVelocity": 3.0,
  "MinTimeBetweenImpacts": 0.25,
  "CollisionDamageMultiplier": 1.0
}
```

- `MinCollisionVelocity` -- Minimum collision velocity required to apply damage.
  - When the collision velocity is lower than this amount, the collision is ignored and no damage is applied.
- `MinTimeBetweenImpacts` -- Minimum time in seconds between drone collision impacts.
  - If the drone collides multiple times in this time frame above the velocity threshold, only the first collision will count. This mechanic is very useful for larger drones since it prevents them from taking unreasonable damage when colliding with multiple objects at once.
- `CollisionDamageMultiplier` -- Damage multiplier to apply to each collision impact above the velocity threshold.
  - When using a value of `1.0`, the damage dealt will match the collision velocity.

## Recommended compatible plugins

Drone balance:
- [Drone Settings](https://umod.org/plugins/drone-settings) -- Allows changing speed, toughness and other properties of RC drones.
- [Targetable Drones](https://umod.org/plugins/targetable-drones) -- Allows RC drones to be targeted by Auto Turrets and SAM Sites.
- [Limited Drone Range](https://umod.org/plugins/limited-drone-range) -- Limits how far RC drones can be controlled from computer stations.

Drone fixes and improvements:
- [Better Drone Collision](https://umod.org/plugins/better-drone-collision) (This plugin) -- Overhauls RC drone collision damage so it's more intuitive.
- [Auto Flip Drones](https://umod.org/plugins/auto-flip-drones) -- Auto flips upside-down RC drones when a player takes control.
- [Drone Hover](https://umod.org/plugins/drone-hover) -- Allows RC drones to hover in place while not being controlled.

Drone attachments:
- [Drone Lights](https://umod.org/plugins/drone-lights) -- Adds controllable search lights to RC drones.
- [Drone Turrets](https://umod.org/plugins/drone-turrets) -- Allows players to deploy auto turrets to RC drones.
- [Drone Storage](https://umod.org/plugins/drone-storage) -- Allows players to deploy a small stash to RC drones.
- [Ridable Drones](https://umod.org/plugins/ridable-drones) -- Allows players to ride RC drones by standing on them or mounting a chair.

## Developer Hooks

#### OnDroneCollisionReplace

```csharp
object OnDroneCollisionReplace(Drone drone)
```

- Called when this plugin is about to alter a drone to replace its collision behavior
  - This happens when drones spawn, and for existing drones when the plugin loads
- Returning `false` will prevent the the drone from being altered
- Returning `null` will result in the default behavior

#### OnDroneCollisionImpact

```csharp
void OnDroneCollisionImpact(Drone drone, Collision collision)
```

- Called when a drone collision occurs above the velocity threshold
- No return behavior
