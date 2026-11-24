using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Remiehzla.Explore
{
    /// <summary>
    /// ⚠️ 临时用的交互检测，只为验证流程。正式的检测（朝向、最近、按键提示 UI）由对方的系统做。
    /// 它做的事：每 0.3 秒扫一遍场景里的可交互物，挑最近的一个显示提示，按 E 交互。
    ///
    /// 对方的系统接进来后，把挂了这个组件的物体删掉即可。
    /// </summary>
    public class TempInteractor : MonoBehaviour
    {
        [Header("交互")]
        public float radius = 1.4f;
        public KeyCode interactKey = KeyCode.E;

        [Tooltip("显示提示文字的 UI，可留空")]
        public Text promptLabel;

        [Tooltip("没有目标时提示文字的默认内容")]
        public string idleText = "";

        readonly List<IInteractable> candidates = new List<IInteractable>();

        IInteractable current;
        float scanTimer;

        void Update()
        {
            scanTimer -= Time.deltaTime;

            if (scanTimer <= 0f)
            {
                scanTimer = 0.3f;
                Rescan();
            }

            current = FindNearest();

            if (promptLabel != null)
                promptLabel.text = current != null ? $"[{interactKey}] {current.Prompt}" : idleText;

            if (current != null && Input.GetKeyDown(interactKey))
                current.Interact(gameObject);
        }

        void Rescan()
        {
            candidates.Clear();

            var all = FindObjectsOfType<MonoBehaviour>();

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is IInteractable interactable && interactable.CanInteract)
                    candidates.Add(interactable);
            }
        }

        IInteractable FindNearest()
        {
            IInteractable best = null;
            float bestDistance = radius * radius;

            for (int i = 0; i < candidates.Count; i++)
            {
                IInteractable candidate = candidates[i];

                if (candidate == null) continue;
                if (!candidate.CanInteract) continue;

                Transform target = candidate.HighlightTarget;
                if (target == null) continue;

                float distance = (target.position - transform.position).sqrMagnitude;

                if (distance > bestDistance) continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }
    }
}
