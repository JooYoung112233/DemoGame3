namespace Demo5.FrontEnd
{
    // The exploration turn sequence as other views see it (written only by FieldTurnReplay; presentation only).
    // Version goes up whenever a new turn sequence starts: markers animate from their old values to the new ones over Progress01.
    public static class FieldTurnPulse { public static bool Playing; public static float Progress01; public static int Turn; public static int Version; }
}
