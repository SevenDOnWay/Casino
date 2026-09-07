using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Assets.Script.TienLen.CardFolder {
    [CreateAssetMenu(fileName = "CardSpriteAtlas", menuName = "TienLen/Card Sprite Atlas")]
    public class TienLenSO : ScriptableObject {

        [Serializable]
        public struct CardSpriteEntry {
            public CardSuit suit;
            public CardRank rank;
            public Sprite sprite;
        }

        [SerializeField] private Sprite cardBack;
        [SerializeField] private List<CardSpriteEntry> cardEntries = new();

        private Dictionary<(CardSuit, CardRank), Sprite> lookupTable;

        public void Initialize() {
            if ( lookupTable != null ) return;
            lookupTable = new Dictionary<(CardSuit, CardRank), Sprite>();

            foreach ( var entry in cardEntries ) {
                lookupTable[(entry.suit, entry.rank)] = entry.sprite;
            }
        }

        public Dictionary<(CardSuit, CardRank), Sprite> GetLookUpTable() {
            Initialize();
            return lookupTable;
        }

        public Sprite GetCardSprite( CardSuit suit, CardRank rank ) {
            Initialize();
            return lookupTable.GetValueOrDefault((suit, rank));
        }

#if UNITY_EDITOR
        // Right-click the component header in the Inspector -> click "Auto Load All Sprites"
        [ContextMenu("Auto Load All Sprites")]
        public void AutoLoadSprites() {
            cardEntries.Clear();

            // 1. Find all Sprites in your specific art folder (no need for a Resources folder)
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Resources/Cards" });

            foreach ( string guid in guids ) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if ( sprite == null ) continue;

                // 2. Parse your naming convention (e.g., "clubs_03", "spades_10", etc.)
                string name = sprite.name.ToLower();

                if ( name.Contains("back") ) {
                    cardBack = sprite;
                    continue;
                }

                if ( TryParseCard(name, out CardSuit suit, out CardRank rank) ) {
                    cardEntries.Add(new CardSpriteEntry {
                        suit = suit,
                        rank = rank,
                        sprite = sprite
                    });
                }
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardSpriteAtlas] Auto-populated {cardEntries.Count} cards successfully!");
        }

        private bool TryParseCard( string filename, out CardSuit suit, out CardRank rank ) {
            suit = default;
            rank = default;

            // Example filenames: "hearts_03", "clubs_king", "spades_ace", "diamonds_02", "hearts_2"
            string[] parts = filename.Split('_');
            if ( parts.Length < 2 ) return false;

            // 1. Parse Suit (e.g. "hearts", "diamonds", "clubs", "spades")
            if ( !Enum.TryParse(parts[0], true, out suit) ) {
                return false;
            }

            // 2. Parse Rank string (handle words, letters, and numbers)
            string rankStr = parts[1].Trim().ToLower();

            int rankValue = rankStr switch {
                "3" or "03" => 3,
                "4" or "04" => 4,
                "5" or "05" => 5,
                "6" or "06" => 6,
                "7" or "07" => 7,
                "8" or "08" => 8,
                "9" or "09" => 9,
                "10"        => 10,
                "j" or "jack"   => 11,
                "q" or "queen"  => 12,
                "k" or "king"   => 13,
                "a" or "ace" or "1" or "01" => 14,
                "2" or "02" or "two" or "deuce" => 15, // Tiến Lên 2 is rank 15
                _ => -1
            };

            // If it wasn't caught by the switch, try parsing as direct integer (fallback)
            if ( rankValue == -1 && int.TryParse(rankStr, out int parsedNum) ) {
                rankValue = parsedNum == 2 ? 15 : parsedNum;
            }

            if ( rankValue >= 3 && rankValue <= 15 ) {
                rank = (CardRank)rankValue;
                return true;
            }

            return false;
        }
#endif
    }
}