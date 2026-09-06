using Assets.Script;
using System.Collections.Generic;
using UnityEngine;

public class CardSpriteAtlas : MonoBehaviour {

    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    [Header("Optional")]
    [SerializeField] private Sprite cardBack;

    private void Awake() {
        LoadAllSprites();
    }

    private void LoadAllSprites() {
        // Loads all Sprite assets located in "Assets/Resources/Cards/"
        Sprite[] loadedSprites = Resources.LoadAll<Sprite>("Cards");

        foreach ( Sprite s in loadedSprites ) {
            // Key is normalized to lowercase (e.g., "clubs_05", "hearts_king")
            string key = s.name.ToLower().Trim();
            if ( !spriteCache.ContainsKey(key) ) {
                spriteCache.Add(key, s);
            }
        }

        Debug.Log($"[CardSpriteAtlas] Successfully cached {spriteCache.Count} card sprites.");

        foreach ( var kvp in spriteCache ) {
            Debug.Log($"[CardSpriteAtlas] Cached Sprite Key: '{kvp.Key}'");
        }
    }


    public Sprite GetCardSprite( Card card ) {
        string suitStr = card.Suit switch
        {
            CardSuit.Spades => "spades",
            CardSuit.Clubs => "clubs",
            CardSuit.Diamonds => "diamonds",
            CardSuit.Hearts => "hearts",
            _ => ""
        };

        string rankStr = card.Rank switch
        {
            CardRank.Three => "03",
            CardRank.Four => "04",
            CardRank.Five => "05",
            CardRank.Six => "06",
            CardRank.Seven => "07",
            CardRank.Eight => "08",
            CardRank.Nine => "09",
            CardRank.Ten => "10",
            CardRank.Jack => "jack",
            CardRank.Queen => "queen",
            CardRank.King => "king",
            CardRank.Ace => "ace",
            CardRank.Two => "02",
            _ => ""
        };

        string lookupKey = $"{suitStr}_{rankStr}";

        if ( spriteCache.TryGetValue(lookupKey, out Sprite found) ) {
            return found;
        }

        // Fallback check in case ranks like 2-9 don't have leading zeros in your pack (e.g. "clubs_5")
        string trimmedRank = rankStr.TrimStart('0');
        string fallbackKey = $"{suitStr}_{trimmedRank}";
        if ( spriteCache.TryGetValue(fallbackKey, out Sprite fallback) ) {
            return fallback;
        }

        Debug.LogWarning($"[CardSpriteAtlas] Sprite not found for key: '{lookupKey}' (or '{fallbackKey}')");
        return null;
    }


    public Sprite GetBackSprite() => cardBack;


}
