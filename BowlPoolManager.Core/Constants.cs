namespace BowlPoolManager.Core
{
    public static class Constants
    {
        public static class Database
        {
            public const string DbName = "BowlMadnessDb";
            
            // New Architecture
            public const string PlayersContainer = "Players";
            public const string SeasonsContainer = "Seasons"; 
            public const string PicksContainer = "Picks";

            // Partition Keys
            public const string DefaultPartitionKey = "/id";
            public const string SeasonPartitionKey = "/seasonId";
        }

        // Helper to standardize the current active season for the migration
        public const string CurrentSeason = "2025";

        public static class Roles
        {
            public const string SuperAdmin = "SuperAdmin";
            public const string Admin = "Admin";
            public const string Player = "Player";
        }

        public static class DocumentTypes
        {
            public const string BowlPool = "BowlPool";
            public const string UserProfile = "UserProfile";
            public const string BowlGame = "BowlGame";
            // NEW
            public const string BracketEntry = "BracketEntry";
        }
    }
}
