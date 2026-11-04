public static class PartyScoreRules
{
    public static int ForRank(int rank) => rank == 1 ? 6 : rank == 2 ? 4 : rank == 3 ? 2 : rank == 4 ? 1 : 0;
}
