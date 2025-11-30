using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ItemLockUI", "AbudMansur", "1.4.0")]
    [Description("UI üzerinden item kilitleme (Modern UI + Item Icons)")]
    public class ItemLockUI : RustPlugin
    {
        private const string PermUse = "itemlockui.use";
        private const string UiPrefix = "ItemLockUI_";
        private const int ItemsPerPage = 5; 

        private Dictionary<string, double> lockedItems = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<ulong, string> tempItem = new Dictionary<ulong, string>();
        private Dictionary<ulong, int> tempTime = new Dictionary<ulong, int>();
        private Dictionary<ulong, float> lastMessageTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, int> currentPage = new Dictionary<ulong, int>(); 

        [PluginReference]
        private Plugin ImageLibrary;

        #region Config
        private Configuration config;

        public class Configuration
        {
            public bool BlockCrafting { get; set; } = true;
            public bool BlockPickup { get; set; } = true;
            public bool BlockEquip { get; set; } = true;
            public bool BlockBeltMove { get; set; } = true;
            public bool BlockDrop { get; set; } = true;
            public bool BlockUse { get; set; } = true;
            public Dictionary<string, double> LockedItems { get; set; } = new Dictionary<string, double>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
                else
                {
                    if (config.LockedItems != null)
                    {
                        lockedItems = new Dictionary<string, double>(config.LockedItems, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                BlockCrafting = true,
                BlockPickup = true,
                BlockEquip = true,
                BlockBeltMove = true,
                BlockDrop = true,
                BlockUse = true,
                LockedItems = new Dictionary<string, double>()
            };
        }

        protected override void SaveConfig()
        {
            config.LockedItems = lockedItems;
            Config.WriteObject(config);
        }
        #endregion

        #region Init
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        private void OnServerInitialized()
        {
            CleanExpiredItems();
            timer.Every(60f, () => CleanExpiredItems());
        }

        private void CleanExpiredItems()
        {
            var expiredItems = new List<string>();
            double currentTime = GetUnixTimestamp();

            foreach (var kv in lockedItems)
            {
                if (kv.Value <= currentTime)
                {
                    expiredItems.Add(kv.Key);
                }
            }

            if (expiredItems.Count > 0)
            {
                foreach (var item in expiredItems)
                {
                    lockedItems.Remove(item);
                }
                SaveConfig();
                Puts($"{expiredItems.Count} adet süresi dolmuş item temizlendi.");
            }
        }
        #endregion

        #region Chat Command
        [ChatCommand("lockui")]
        private void CmdLockUI(BasePlayer player, string cmd, string[] args)
        {
            if (player == null) return;
            if (!player.IsAdmin && !permission.UserHasPermission(player.userID.ToString(), PermUse))
            {
                SendReply(player, "<color=orange>Bu menüyü kullanmak için yetkiniz yok.</color>");
                return;
            }

            tempItem[player.userID] = "";
            tempTime[player.userID] = 0;
            currentPage[player.userID] = 0; 
            CreateUi(player);
        }
        #endregion

        #region UI
        private void CreateUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiPrefix + player.userID);
            var elements = new CuiElementContainer();

            string blur = elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.4", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true
            }, "Overlay", UiPrefix + player.userID);

            string mainPanel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.15 0.98" },
                RectTransform = { AnchorMin = "0.25 0.15", AnchorMax = "0.75 0.85" }
            }, blur);

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.85 0.35 0.25 1" },
                RectTransform = { AnchorMin = "0 0.96", AnchorMax = "1 1" }
            }, mainPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = "🔐", FontSize = 32, Align = TextAnchor.MiddleLeft, Color = "0.85 0.35 0.25 1" },
                RectTransform = { AnchorMin = "0.03 0.90", AnchorMax = "0.15 0.96" }
            }, mainPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = "ITEM LOCK MANAGER", FontSize = 24, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.12 0.90", AnchorMax = "0.85 0.96" }
            }, mainPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = "İtem Locking Plugin AbudMansur", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.12 0.88", AnchorMax = "0.85 0.91" }
            }, mainPanel);

            string closeBtn = elements.Add(new CuiButton
            {
                Button = { Color = "0.8 0.2 0.2 0.8", Command = "itemlockui.close" },
                RectTransform = { AnchorMin = "0.94 0.90", AnchorMax = "0.98 0.96" },
                Text = { Text = "", FontSize = 0 }
            }, mainPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = "✕", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, closeBtn);

            string inputSection = elements.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.08 0.1 0.7" },
                RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.95 0.86" }
            }, mainPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = "ITEM SHORTNAME", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "0.85 0.35 0.25 1" },
                RectTransform = { AnchorMin = "0.03 0.60", AnchorMax = "0.48 0.90" }
            }, inputSection);

            string itemInputBg = elements.Add(new CuiPanel
            {
                Image = { Color = "0.95 0.95 0.98 0.95" },
                RectTransform = { AnchorMin = "0.03 0.15", AnchorMax = "0.48 0.55" }
            }, inputSection);

            elements.Add(new CuiElement
            {
                Parent = itemInputBg,
                Name = "ItemLockInput_Item",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = tempItem[player.userID],
                        FontSize = 13,
                        Color = "0.1 0.1 0.1 1",
                        Align = TextAnchor.MiddleLeft,
                        Command = "itemlockui.setitem",
                        CharsLimit = 50
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.04 0",
                        AnchorMax = "0.96 1"
                    }
                }
            });

            elements.Add(new CuiLabel
            {
                Text = { Text = "DURATION (HOURS)", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "0.85 0.35 0.25 1" },
                RectTransform = { AnchorMin = "0.52 0.60", AnchorMax = "0.97 0.90" }
            }, inputSection);

            string timeInputBg = elements.Add(new CuiPanel
            {
                Image = { Color = "0.95 0.95 0.98 0.95" },
                RectTransform = { AnchorMin = "0.52 0.15", AnchorMax = "0.97 0.55" }
            }, inputSection);

            elements.Add(new CuiElement
            {
                Parent = timeInputBg,
                Name = "ItemLockInput_Time",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = tempTime[player.userID].ToString(),
                        FontSize = 13,
                        Color = "0.1 0.1 0.1 1",
                        Align = TextAnchor.MiddleLeft,
                        Command = "itemlockui.settime",
                        CharsLimit = 10
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.04 0",
                        AnchorMax = "0.96 1"
                    }
                }
            });

            string lockBtn = elements.Add(new CuiButton
            {
                Button = { Color = "0.2 0.7 0.3 0.9", Command = "itemlockui.confirm" },
                RectTransform = { AnchorMin = "0.35 0.68", AnchorMax = "0.65 0.73" },
                Text = { Text = "", FontSize = 0 }
            }, mainPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = "🔒  LOCK", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, lockBtn);

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.3 0.3 0.35 0.5" },
                RectTransform = { AnchorMin = "0.05 0.66", AnchorMax = "0.95 0.665" }
            }, mainPanel);

            
            int page = currentPage.ContainsKey(player.userID) ? currentPage[player.userID] : 0;
            int totalPages = Mathf.CeilToInt((float)lockedItems.Count / ItemsPerPage);
            if (totalPages == 0) totalPages = 1;

            elements.Add(new CuiLabel
            {
                Text = { Text = "LOCKED ITEMS", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.9" },
                RectTransform = { AnchorMin = "0.05 0.62", AnchorMax = "0.4 0.65" }
            }, mainPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = $"Page {page + 1}/{totalPages} | Total: {lockedItems.Count}", FontSize = 11, Align = TextAnchor.MiddleRight, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.4 0.62", AnchorMax = "0.95 0.65" }
            }, mainPanel);

            string scrollContainer = elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.05 0.10", AnchorMax = "0.95 0.61" }
            }, mainPanel);

            
            if (totalPages > 1)
            {
                
                if (page > 0)
                {
                    string prevBtn = elements.Add(new CuiButton
                    {
                        Button = { Color = "0.2 0.5 0.7 0.8", Command = "itemlockui.prevpage" },
                        RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.25 0.08" },
                        Text = { Text = "◄ Previous", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    }, mainPanel);
                }

                
                if (page < totalPages - 1)
                {
                    string nextBtn = elements.Add(new CuiButton
                    {
                        Button = { Color = "0.2 0.5 0.7 0.8", Command = "itemlockui.nextpage" },
                        RectTransform = { AnchorMin = "0.75 0.04", AnchorMax = "0.95 0.08" },
                        Text = { Text = "Next ►", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    }, mainPanel);
                }
            }

            
            float startY = 0.95f;
            float itemHeight = 0.18f;
            float spacing = 0.02f;

            var itemList = lockedItems.Skip(page * ItemsPerPage).Take(ItemsPerPage).ToList();
            int index = 0;

            foreach (var kv in itemList)
            {
                string shortname = kv.Key;
                double remaining = kv.Value - GetUnixTimestamp();
                if (remaining < 0) remaining = 0;

                float currentY = startY - (index * (itemHeight + spacing));

                string itemCard = elements.Add(new CuiPanel
                {
                    Image = { Color = "0.15 0.15 0.18 0.9" },
                    RectTransform = { AnchorMin = $"0 {currentY - itemHeight}", AnchorMax = $"1 {currentY}" }
                }, scrollContainer);

                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.85 0.35 0.25 0.8" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.008 1" }
                }, itemCard);

                string imageUrl = GetItemImage(shortname);
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    elements.Add(new CuiElement
                    {
                        Parent = itemCard,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = imageUrl,
                                Color = "1 1 1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.02 0.2",
                                AnchorMax = "0.12 0.8"
                            }
                        }
                    });
                }
                else
                {
                    elements.Add(new CuiLabel
                    {
                        Text = { Text = "📦", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.85 0.35 0.25 1" },
                        RectTransform = { AnchorMin = "0.02 0.2", AnchorMax = "0.12 0.8" }
                    }, itemCard);
                }

                string displayName = GetItemDisplayName(shortname);
                elements.Add(new CuiLabel
                {
                    Text = { Text = displayName, FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.14 0.60", AnchorMax = "0.60 0.85" }
                }, itemCard);

                elements.Add(new CuiLabel
                {
                    Text = { Text = shortname, FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "0.5 0.5 0.5 1" },
                    RectTransform = { AnchorMin = "0.14 0.42", AnchorMax = "0.60 0.58" }
                }, itemCard);

                string timeText = FormatTime((int)remaining);
                elements.Add(new CuiLabel
                {
                    Text = { Text = $"⏱️ Remaining: {timeText}", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.7 0.9 0.7 1" },
                    RectTransform = { AnchorMin = "0.14 0.15", AnchorMax = "0.60 0.38" }
                }, itemCard);

                string timeBox = elements.Add(new CuiPanel
                {
                    Image = { Color = "0.08 0.08 0.1 0.8" },
                    RectTransform = { AnchorMin = "0.63 0.20", AnchorMax = "0.78 0.80" }
                }, itemCard);

                elements.Add(new CuiLabel
                {
                    Text = { Text = "⏱️", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0.85 0.35 0.25 1" },
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
                }, timeBox);

                elements.Add(new CuiLabel
                {
                    Text = { Text = timeText, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5" }
                }, timeBox);

                string unlockBtn = elements.Add(new CuiButton
                {
                    Button = { Color = "0.2 0.7 0.3 0.85", Command = $"itemlockui.unlock {shortname}" },
                    RectTransform = { AnchorMin = "0.79 0.20", AnchorMax = "0.96 0.80" },
                    Text = { Text = "UNLOCK", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, itemCard);

                index++;
            }

            if (lockedItems.Count == 0)
            {
                elements.Add(new CuiLabel
                {
                    Text = { Text = "🔓", FontSize = 48, Align = TextAnchor.MiddleCenter, Color = "0.3 0.3 0.35 0.5" },
                    RectTransform = { AnchorMin = "0.4 0.45", AnchorMax = "0.6 0.65" }
                }, scrollContainer);

                elements.Add(new CuiLabel
                {
                    Text = { Text = "No locked items yet", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.5 0.5 0.5 1" },
                    RectTransform = { AnchorMin = "0.2 0.35", AnchorMax = "0.8 0.43" }
                }, scrollContainer);
            }

            CuiHelper.AddUi(player, elements);
        }

        private string FormatTime(int seconds)
        {
            if (seconds < 60) return $"{seconds}s";
            if (seconds < 3600) return $"{seconds / 60}m {seconds % 60}s";
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            return $"{hours}h {minutes}m";
        }

        private string GetItemImage(string shortname)
        {
            if (ImageLibrary == null) return null;
            return (string)ImageLibrary?.Call("GetImage", shortname);
        }

        private string GetItemDisplayName(string shortname)
        {
            var itemDef = ItemManager.FindItemDefinition(shortname);
            if (itemDef != null)
            {
                return itemDef.displayName.english.ToUpper();
            }
            return shortname.ToUpper();
        }
        #endregion

        #region Console Commands
        [ConsoleCommand("itemlockui.setitem")]
        private void SetItem(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;
            string input = arg?.FullString ?? "";
            input = input.Replace("itemlockui.setitem", "").Trim();
            tempItem[player.userID] = input;
            CreateUi(player);
        }

        [ConsoleCommand("itemlockui.settime")]
        private void SetTime(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;
            string input = arg?.FullString ?? "";
            input = input.Replace("itemlockui.settime", "").Trim();
            if (int.TryParse(input, out int hours))
            {
                tempTime[player.userID] = Math.Max(0, hours);
                CreateUi(player);
            }
        }

        [ConsoleCommand("itemlockui.confirm")]
        private void Confirm(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;
            string shortname = tempItem.ContainsKey(player.userID) ? tempItem[player.userID] : "";
            int hours = tempTime.ContainsKey(player.userID) ? tempTime[player.userID] : 0;

            if (string.IsNullOrEmpty(shortname) || hours <= 0)
            {
                SendReply(player, "<color=yellow>⚠️ Please enter valid item and duration.</color>");
                return;
            }

            int seconds = hours * 3600;
            lockedItems[shortname] = GetUnixTimestamp() + seconds;
            SaveConfig();
            SendReply(player, $"<color=#2ECC71>✅ {shortname}</color> locked successfully! ({hours}h)");

            tempItem[player.userID] = "";
            tempTime[player.userID] = 0;
            CreateUi(player);
        }

        [ConsoleCommand("itemlockui.unlock")]
        private void Unlock(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            string shortname = arg?.GetString(0);
            if (player == null || string.IsNullOrEmpty(shortname)) return;

            if (lockedItems.ContainsKey(shortname))
            {
                lockedItems.Remove(shortname);
                SaveConfig();
                SendReply(player, $"<color=#2ECC71>🔓 {shortname}</color> unlocked successfully!");
                CreateUi(player);
            }
        }

        [ConsoleCommand("itemlockui.prevpage")]
        private void PrevPage(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;

            if (currentPage.ContainsKey(player.userID))
                currentPage[player.userID] = Math.Max(0, currentPage[player.userID] - 1);
            else
                currentPage[player.userID] = 0;

            CreateUi(player);
        }

        [ConsoleCommand("itemlockui.nextpage")]
        private void NextPage(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;

            int totalPages = Mathf.CeilToInt((float)lockedItems.Count / ItemsPerPage);

            if (currentPage.ContainsKey(player.userID))
                currentPage[player.userID] = Math.Min(totalPages - 1, currentPage[player.userID] + 1);
            else
                currentPage[player.userID] = 0;

            CreateUi(player);
        }

        [ConsoleCommand("itemlockui.close")]
        private void Close(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;
            CuiHelper.DestroyUi(player, UiPrefix + player.userID);
        }
        #endregion

        #region Hooks
        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            if (!config.BlockCrafting) return null;
            if (bp == null || itemCrafter == null) return null;
            var shortname = bp.targetItem?.shortname ?? "";
            if (IsBlocked(shortname, out double remaining))
            {
                var player = itemCrafter.GetComponent<BasePlayer>();
                if (player != null && CanShowMessage(player))
                    player.ChatMessage($"<color=#E74C3C>🔒 {shortname} is locked! Remaining: {FormatTime((int)remaining)}</color>");
                return false;
            }
            return null;
        }

        private object CanPickupItem(BasePlayer player, Item item)
        {
            if (!config.BlockPickup) return null;
            if (player == null || item == null) return null;
            var shortname = item.info?.shortname ?? "";
            if (IsBlocked(shortname, out double remaining))
            {
                if (CanShowMessage(player))
                    player.ChatMessage($"<color=#E74C3C>🔒 {shortname} is locked! Remaining: {FormatTime((int)remaining)}</color>");
                return false;
            }
            return null;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            if (!config.BlockEquip) return null;
            if (item == null || inventory == null) return null;
            var shortname = item.info?.shortname ?? "";
            if (IsBlocked(shortname, out double remaining))
            {
                var player = inventory.GetComponent<BasePlayer>();
                if (player != null && CanShowMessage(player))
                    player.ChatMessage($"<color=#E74C3C>🔒 {shortname} is locked! Cannot equip. Remaining: {FormatTime((int)remaining)}</color>");
                return false;
            }
            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory playerInventory, ItemContainerId targetContainerId, int targetSlot, int amount)
        {
            if (!config.BlockBeltMove) return null;
            if (item == null || playerInventory == null) return null;
            var shortname = item.info?.shortname ?? "";

            if (IsBlocked(shortname, out double remaining))
            {
                if (targetSlot >= 0 && targetSlot <= 5 && targetContainerId.Value == playerInventory.containerBelt.uid.Value)
                {
                    var player = playerInventory.GetComponent<BasePlayer>();
                    if (player != null && CanShowMessage(player))
                        player.ChatMessage($"<color=#E74C3C>🔒 {shortname} is locked! Cannot move to belt. Remaining: {FormatTime((int)remaining)}</color>");
                    return false;
                }
            }
            return null;
        }

        private object CanDropActiveItem(BasePlayer player)
        {
            if (!config.BlockDrop) return null;
            if (player == null) return null;
            var activeItem = player.GetActiveItem();
            if (activeItem == null) return null;

            var shortname = activeItem.info?.shortname ?? "";
            if (IsBlocked(shortname, out double remaining))
            {
                if (CanShowMessage(player))
                    player.ChatMessage($"<color=#E74C3C>🔒 {shortname} is locked! Cannot drop. Remaining: {FormatTime((int)remaining)}</color>");
                return false;
            }
            return null;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!config.BlockUse) return null;
            if (item == null || player == null) return null;
            var shortname = item.info?.shortname ?? "";

            if (IsBlocked(shortname, out double remaining))
            {
                if (CanShowMessage(player))
                    player.ChatMessage($"<color=#E74C3C>🔒 {shortname} is locked! Cannot use. Remaining: {FormatTime((int)remaining)}</color>");
                return false;
            }
            return null;
        }
        #endregion

        #region Helpers
        private bool IsBlocked(string shortname, out double remaining)
        {
            remaining = 0;
            if (string.IsNullOrEmpty(shortname)) return false;
            if (lockedItems.TryGetValue(shortname, out double expiry))
            {
                if (expiry <= GetUnixTimestamp())
                {
                    lockedItems.Remove(shortname);
                    SaveConfig();
                    return false;
                }
                remaining = expiry - GetUnixTimestamp();
                return true;
            }
            return false;
        }

        private bool CanShowMessage(BasePlayer player)
        {
            if (player == null) return false;

            float currentTime = Time.realtimeSinceStartup;
            if (lastMessageTime.ContainsKey(player.userID))
            {
                if (currentTime - lastMessageTime[player.userID] < 2f)
                {
                    return false;
                }
            }

            lastMessageTime[player.userID] = currentTime;
            return true;
        }

        private double GetUnixTimestamp()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
        #endregion
    }
}