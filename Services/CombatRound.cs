public class CombatRound
{
    public int AttackerRoll { get; set; }
    public int DefenderRoll { get; set; }
    public int AttackerScore { get; set; }
    public int DefenderScore { get; set; }
    public bool AttackerWon { get; set; }
    public bool Tie { get; set; }
    public int AttackerLifeAfter { get; set; }
    public int DefenderLifeAfter { get; set; }
}