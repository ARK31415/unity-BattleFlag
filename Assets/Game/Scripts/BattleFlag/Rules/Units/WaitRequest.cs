using System;

namespace BF.Game.Battle.Rules.Units
{
    /// <summary>
    /// 等待行动请求。只表达等待单位的规则身份；剩余 AP 为 0 时必须返回失败。
    /// </summary>
    public readonly struct WaitRequest
    {
        /// <summary>
        /// 创建等待请求。
        /// </summary>
        /// <param name="runtimeId">执行等待单位的运行时身份。</param>
        public WaitRequest(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
                throw new ArgumentException("RuntimeId 不能为空。", nameof(runtimeId));

            RuntimeId = runtimeId;
        }

        /// <summary>执行等待单位的运行时身份。</summary>
        public string RuntimeId { get; }
    }
}
