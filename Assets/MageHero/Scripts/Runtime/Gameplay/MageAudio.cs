using UnityEngine;

namespace MageHero
{
    public static class MageAudio
    {
        static AudioClip attackClip;

        public static void PlayLevelAmbience(LevelInfo levelInfo)
        {
            string resourceName;
            switch (levelInfo.levelId)
            {
                case "desierto": resourceName = "Desierto"; break;
                case "otono": resourceName = "Otono"; break;
                case "primavera": resourceName = "Primavera"; break;
                default:
                    Debug.LogWarning($"No ambience is configured for level '{levelInfo.levelId}'.", levelInfo);
                    return;
            }

            var clip = Resources.Load<AudioClip>("Audio/" + resourceName);
            if (clip == null)
            {
                Debug.LogError($"Could not load ambience clip 'Audio/{resourceName}'.", levelInfo);
                return;
            }

            var source = levelInfo.GetComponent<AudioSource>();
            if (source == null) source = levelInfo.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.35f;
            source.Play();
        }

        public static void PlayAttack(GameObject player)
        {
            if (attackClip == null) attackClip = Resources.Load<AudioClip>("Audio/AtaqueMago");
            if (attackClip == null)
            {
                Debug.LogError("Could not load attack clip 'Audio/AtaqueMago'.", player);
                return;
            }

            var source = player.GetComponent<AudioSource>();
            if (source == null)
            {
                source = player.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.PlayOneShot(attackClip, 0.85f);
        }
    }
}
