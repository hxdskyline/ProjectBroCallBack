namespace Camp
{
    /// <summary>
    /// 战场区域高亮模式
    /// 内圈=Layer3, 中圈=Layer2, 外圈=Layer1
    /// </summary>
    public enum ZoneHighlightType
    {
        None,
        InnerGreenRestRed,
        MiddleGreenRestRed,
        OuterGreenRestRed,
        InnerRedRestGreen,
        MiddleRedRestGreen,
        OuterRedRestGreen,
        AllGreen,
        AllRed
    }
}
