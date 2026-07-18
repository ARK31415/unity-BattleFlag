namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// 输入系统的基础上下文，同一时间只应有一个基础上下文生效。
    /// </summary>
    public enum BFInputBaseContext
    {
        None = 0,
        Battle = 1,
        Dialogue = 2,
        MainMenu = 3,
        Cutscene = 4
    }
}
