# Base implementation done by SunnyValleyStudio:
https://github.com/SunnyValleyStudio/Unity_Procedural_Dungeon_binary_space_partitioning/tree/master

After analyzing and implementing this solution, we made the following changes

# Floor height

We started by adding a configurable wall script and a floor height so we can decide the height of the walls

then added another generation of floor to act as the ceiling.

# Floor count

Added a variable for floor count to customize the height of the dungeon and generated multiple floors

# WFC

each room creates a 3D grid which the dungeon generator seeds with doors where there is a corridor (there's a bug there with the corridors, so it not always works). it then collapses using an entorpy function and adjacency rules with it's possible options in it's domain (i.e. air, column, floor, wall, wall with column, and corner wall). 

# additional notes

* Added validation to corridors so they would work better with WFC
* Added the ability to add special room types and add objects to those rooms
* Define special rooms that can only be of certain size in the dungeon and have a limit on how many of them can be
* Create a portal at the end of each floor in a room far away from the player to teleport you to the next floor
* dyanmically change the height of the dungeon