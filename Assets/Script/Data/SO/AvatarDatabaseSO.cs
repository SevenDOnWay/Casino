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

        public Sprite GetAvatarSprite( int id ) {
            if ( avatars == null || avatars.Length == 0 ) return null;

            for ( int i = 0; i < avatars.Length; i++ ) {
                if ( avatars[i].avatarId == id ) {
                    return avatars[i].avatarSprite;
                }
            }

            return avatars[0].avatarSprite;
        }

        public int TotalAvatars => avatars != null ? avatars.Length : 0;
    }
}
