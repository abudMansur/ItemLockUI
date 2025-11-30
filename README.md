# ItemLockUI
![image](https://github.com/user-attachments/assets/d5de2a84-22cd-499a-8192-5d64eb29cce5) ![image](https://github.com/user-attachments/assets/d0464b08-5984-4cf4-be82-ce77c018a4ca)

ItemLockUI
Description ItemLockUI is a Rust plugin that allows server administrators to temporarily lock specific items using a modern, easy-to-use user interface. It prevents players from crafting, picking up, equipping, or using restricted items for a specific duration.

This plugin is ideal for wipe days (e.g., locking C4 or high-tier guns for the first 24 hours) or for temporarily disabling specific items without removing them from the game entirely. The system includes an automatic timer that unlocks items once the duration expires.

Features

Modern GUI: A clean, visual interface for managing locks (supports item icons via ImageLibrary).

Comprehensive Blocking: Prevents players from:

Crafting

Picking up

Equipping

Moving to Belt/Hotbar

Dropping

Using (e.g., eating food, using syringes)

Timed Restrictions: Locks are temporary; you set the duration (in hours), and they automatically expire.

Real-time Feedback: Players receive an informative chat message with the remaining lock time if they attempt to interact with a locked item.

Persistent Data: Locked items and timers are saved, so restrictions remain active even after a server restart.

Installation

Download the ItemLockUI.cs file.

Upload it to your server's oxide/plugins folder.

Recommended: Ensure the ImageLibrary plugin is installed on your server. ItemLockUI uses it to fetch and display item icons in the menu. If not installed, a default box icon (📦) will be used.

Permissions This plugin uses a single permission for access to the administration menu:

itemlockui.use — Grants access to the /lockui command and the management interface. (Note: Server admins usually have this access by default.)

Commands

/lockui — Opens the Item Lock Manager interface.

Configuration The configuration file can be found at oxide/config/ItemLockUI.json. You can toggle specific restrictions on or off depending on your server's needs.

JSON

{
  "BlockCrafting": true,    // Blocks crafting of the locked item
  "BlockPickup": true,      // Blocks picking up the locked item from the ground/world
  "BlockEquip": true,       // Blocks equipping the item (wearing/holding)
  "BlockBeltMove": true,    // Blocks moving the item to the hotbar (belt)
  "BlockDrop": true,        // Blocks dropping the item from inventory
  "BlockUse": true,         // Blocks "using" the item (consuming, etc.)
  "LockedItems": {}         // Stores currently locked items (Do not edit manually)
}
