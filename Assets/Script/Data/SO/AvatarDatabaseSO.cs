using UnityEngine;

namespace Assets.Script.Data.SO {
    [CreateAssetMenu(fileName = "AvatarDatabase", menuName = "TienLen/Avatar Database")]
    public class AvatarDatabaseSO : ScriptableObject {
        [System.Serializable]
        public struct AvatarEntry {
            public int avatarId;
            public string avatarName;
            public Sprite avatarSprite;
        }

        [SerializeField] private AvatarEntry[] avatars;


        public bool TryGetAvatarSprite( int id, out Sprite sprite ) {
            sprite = null;
            if ( avatars == null || avatars.Length == 0 ) return false;

            for ( int i = 0; i < avatars.Length; i++ ) {
                if ( avatars[i].avatarId == id ) {
                    sprite = avatars[i].avatarSprite;
                    return true;
                }
            }

            return false;
        }

        public int TotalAvatars => avatars != null ? avatars.Length : 0;
    }
}
