public class CombatResult
{
    public Unit Attacker { get; set; }
    public Unit Defender { get; set; }
    public TilePosition AttackerOriginalPosition { get; set; }
    public List<CombatRound> Rounds { get; set; }
    public bool AttackerWon { get; set; }
    public bool DefenderWon { get; set; }
    public bool AttackerEscaped { get; set; }
    
    public int AttackerInitialLife { get; set; }
    public int DefenderInitialLife { get; set; }

    public Structure DefendingStructure { get; set; }
    public bool StructureDestroyed { get; set; }

    public CombatResult()
    {
        Rounds = new List<CombatRound>();
    }
}