namespace ManiApi.Models
{
    public enum MoveType
    {
        PLANNED,   // ← JAUNA VĒRTĪBA, lai atbilst DB
        DETAILED,
        ASSEMBLY,
        FINISHING,
        STOCK,
        SCRAP,
        OUT      // ← JAUNS: pārdošana/izdošana no noliktavas -> būs vajadzigs SOLD vai OUT 19.01.2025
    }
}
