# Introduction
Adds a new survivor: Infernus from the Cursed Apple.  
  
If people are interested in this project I'll see about importing other characters from Deadlock into RoR2 as well.  


## Skills
| Type | Name | Description | 
|---|---|-----|
|Passive| Afterburn | Incendiary Remarks applies stacks of buildup on hit, afterburn deals 200% damage per second. Intensifies every ten stacks. |
|Primary| Incenidary Remarks | Fire flames for 36% damage. |
|Secondary| Napalm | Eject napalm to coat enemies dealing 250% damage. The Napalm status effect increases damage taken by 30%. |
|Utility| Flame Dash | Dash swiftly leaving behind a trail of flames that deals 300% damage every second. |
|Special| Concussive Combustion | Become into a living bomb, dealing 800% damage in a radius. |


> Disclaimer: The base was sourced from the Henry Tutorial, and made possible through the modding discords support.  
> Head to the [Wiki](https://github.com/ArcPh1r3/HenryTutorial/wiki) if you're interested in implementing your own character.  
> Also, consider going to the official modding discord for help, there are wonderful people who share resources in there.  
  
  
## Changelog
PendingFor2.0.5: Multiplayer Fix
    To do (When player isn't host):
    Flame Dash does not damage enemies
    Afterburn buildup doesn't apply on primary fire hit
    Concussive Combustion works as intended, but needs a nerf
    On Death player is stuck in limbo, unable to respawn, become a drone, or spectate (Addendum: If host dies, death completes regardless)
    Visuals work as intended across network in 2 player mode
    If Host:
    No issues :) (No vis, ui or vfx issues)

    Work on Fun Factor


PendingFor2.0.4
 - v2.0.4 -
    To do:
    Add startStop animation for when player movement hits zero, returning to standing, massively improves jank
    Add utility start animation for when the player starts running, then returns to standing, improves character immersion
    Make shots appear from fingertip rather than center of mass
    Make animations play forward rather than tilted to the right
    Make arm not tilt with every shot
    Make ammo used as resource on primary skill (Emulating Reloading)
    Make default pose an arm up pose for character select, facing camera as well
    Add physics for hair bones, emulating his head flame in Deadlock
    Make larger hair brighter, and only enable during special use
    Raise infernus up by 0.2f for his idle, his feet sink into the ground on idle
    Fix hoopoo feather giving infinite jumps during Flame Dash
    Add item placements on character
PendingFor2.0.4End
    

 - v2.0.3 - 
    Updated animations
    Updated player model
    Updated dash logic
    Updated special logic
    Updated texturing
    New visual effects
    Updated damage type logic
 
 - v2.0.2 - Updated UI colors and added player icon for Infernus  
   
 - v2.0.1 - Updated readme bottom text to account for found animations
  
 - v2.0.0 - 
    Added new Infernus body that contains original animations from Deadlock  
    Removed reliance on HenryMod vfx  
    Minor readme updates  
    Balanced higher early game fire rate with slashed Afterburn damage  
    Wind up added to Concussive Combustion  
    Reset duration on Afterburn with Flame Dash procs  
    Added Flame Canister compatability with Infernus' Afterburn  
    Rewrote Napalm and Concussive Combustion functionality  
    Added Infernus idle animations to select screen  
    Added dash cancel (Keybound to Space) for Flame Dash  
    Opened feedback and balance reccomendations on ROR2's official modding discord  
  
 - v1.1.9 - Added directional controls for napalm, flame dash collider removed, double hit bug on napalm rectified
  
 - v1.1.7 - Fixed descriptions to match in game language, updated ReadMe
  
 - v1.1.5 - Actually fixed the file structure
    
 - v1.1.3 - Infernus published rigged but missing animations  
    
    
> If you are interested in giving feedback, messaging me in the modding discord will suffice
> My tag in that space is matterwoven
    
Thank you for downloading this passion project!  
