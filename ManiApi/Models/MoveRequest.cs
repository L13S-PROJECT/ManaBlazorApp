namespace ManiApi.Models
{
    public class MoveRequest
    {
        public int Version_ID { get; set; }   // uz kuru produkta VERSIJU attiecas kustība
        public MoveType From { get; set; }    // no kāda posma ņemam (piem., DETAILED)
        public MoveType To { get; set; }      // uz kādu posmu liekam (piem., ASSEMBLY)
        public int Qty { get; set; }          // daudzums (pozitīvs skaitlis)
        public int? Task_ID { get; set; }     // ja kustība saistīta ar konkrētu uzdevumu
        public DateTime? Created_At { get; set; } // ja gribi iedot konkrētu datumu; ja null -> Now (to uzliksim API pusē)
    }
}
