namespace BowlPoolManager.Core
{
    public static class Constants
    {
        public static class Database
        {
            public const string DbName = "BowlMadnessDb";
            public const string ContainerName = "MainContainer";
            public const string PartitionKeyPath = "/id";
        }

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
