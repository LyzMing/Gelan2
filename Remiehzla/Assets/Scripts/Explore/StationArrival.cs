using System.Collections;
using UnityEngine;
using Remiehzla.Flow;

namespace Remiehzla.Explore
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StationArrival : MonoBehaviour
    {
        public string orangesId;
        public Sprite[] arrivalFrames;
        public float frameSeconds = 0.55f;
        SpriteRenderer platform;
        Vector3 mapCenter;
        float mapWidth;

        void Awake()
        {
            platform = GetComponent<SpriteRenderer>();
            mapCenter = platform.bounds.center;
            mapWidth = platform.bounds.size.x;
        }

        void OnEnable() => GameEvents.ItemCollected += OnCollected;
        void OnDisable() => GameEvents.ItemCollected -= OnCollected;

        void Start()
        {
            if (ProgressService.HasCollected(orangesId) && arrivalFrames.Length > 0)
                Show(arrivalFrames[arrivalFrames.Length - 1]);
        }

        void OnCollected(string itemId)
        {
            if (itemId == orangesId) StartCoroutine(ReturnTrain());
        }

        IEnumerator ReturnTrain()
        {
            foreach (var frame in arrivalFrames)
            {
                Show(frame);
                yield return new WaitForSeconds(frameSeconds);
            }
        }

        void Show(Sprite frame)
        {
            if (frame == null) return;
            platform.sprite = frame;
            transform.localScale = Vector3.one * (mapWidth / frame.bounds.size.x);
            transform.position += mapCenter - platform.bounds.center;
        }
    }
}
