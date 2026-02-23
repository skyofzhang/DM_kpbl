using UnityEngine;
using CapybaraDuel.Entity;

namespace CapybaraDuel.VFX
{
    /// <summary>
    /// 阵营菲涅尔颜色设置器 - 通过 MaterialPropertyBlock 设置单位的阵营颜色
    /// 通用组件，适用于所有单位类型（Kpbl/201_Sheep/BigSheep及未来新单位）
    /// </summary>
    public class CapybaraCampEffect : MonoBehaviour
    {
        private static readonly Color LEFT_CAMP_COLOR = new Color(1f, 0.55f, 0f);      // 橙色
        private static readonly Color RIGHT_CAMP_COLOR = new Color(0.68f, 1f, 0.18f);   // 黄绿

        private static readonly int _CampColorID = Shader.PropertyToID("_CampColor");

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;

        /// <summary>设置阵营颜色</summary>
        /// <remarks>
        /// 当前所有单位使用 URP/Lit shader，不支持 _CampColor 属性。
        /// 在 URP/Lit 上调用 SetPropertyBlock() 会破坏 SRP Batcher 兼容性，
        /// 导致模型在 Play Mode 中完全不可见。
        /// 因此目前此方法为空实现（no-op）。
        /// 未来切换到支持 _CampColor 的自定义 shader 后可重新启用。
        /// </remarks>
        public void SetCamp(Camp camp)
        {
            // === 完全禁用 MaterialPropertyBlock ===
            // URP/Lit 不支持 _CampColor，调用 SetPropertyBlock 会破坏 SRP Batcher
            // 导致模型不可见。在换到自定义 shader 之前，这里什么都不做。
            return;
        }
    }
}
