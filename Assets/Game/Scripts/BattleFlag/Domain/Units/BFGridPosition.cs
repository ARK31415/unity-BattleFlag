using System;

namespace BF.Game.Battle.Domain.Units
{
    /// <summary>
    /// 规则层使用的网格坐标。
    ///
    /// 该值类型不引用 Unity Vector2Int；Unity 坐标转换由适配层负责。
    /// </summary>
    public readonly struct BFGridPosition : IEquatable<BFGridPosition>
    {
        /// <summary>
        /// 创建一个规则网格坐标。
        /// </summary>
        /// <param name="x">横向坐标。</param>
        /// <param name="y">纵向坐标。</param>
        public BFGridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>横向坐标。</summary>
        public int X { get; }

        /// <summary>纵向坐标。</summary>
        public int Y { get; }

        /// <inheritdoc />
        public bool Equals(BFGridPosition other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is BFGridPosition other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        /// <summary>比较两个规则网格坐标是否相等。</summary>
        public static bool operator ==(BFGridPosition left, BFGridPosition right)
        {
            return left.Equals(right);
        }

        /// <summary>比较两个规则网格坐标是否不等。</summary>
        public static bool operator !=(BFGridPosition left, BFGridPosition right)
        {
            return !left.Equals(right);
        }
    }
}
