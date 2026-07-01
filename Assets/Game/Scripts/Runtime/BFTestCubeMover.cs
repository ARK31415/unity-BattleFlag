using UnityEngine;

/// <summary>
/// 测试用方块控制器，使方块在左右两侧之间来回移动。
///
/// 职责边界：
/// - 负责在指定的两个位置之间往复移动。
/// - 不负责输入响应或与其他系统交互。
///
/// Inspector 配置说明：
/// - 需要设置 leftBound 和 rightBound 来定义移动范围。
/// - speed 控制移动速度（单位/秒）。
/// </summary>
public sealed class BFTestCubeMover : MonoBehaviour
{
    // 移动范围的左右边界
    [SerializeField] private float _leftBound = -5f;
    [SerializeField] private float _rightBound = 5f;

    // 移动速度（单位/秒）
    [SerializeField] private float _speed = 3f;

    // 当前是否向右移动
    private bool _movingRight = true;

    private void Update()
    {
        // 根据当前方向计算这一帧的移动量
        float direction = _movingRight ? 1f : -1f;
        float step = _speed * Time.deltaTime;
        transform.Translate(direction * step, 0f, 0f);

        // 到达边界时反转方向
        if (_movingRight && transform.position.x >= _rightBound)
        {
            _movingRight = false;
        }
        else if (!_movingRight && transform.position.x <= _leftBound)
        {
            _movingRight = true;
        }
    }
}
