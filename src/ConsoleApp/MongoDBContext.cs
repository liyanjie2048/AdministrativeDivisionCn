using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace ConsoleApp
{
    class MongoDBContext
    {
        readonly IMongoDatabase database;
        public MongoDBContext(string connectionString)
        {
            this.database = new MongoClient(connectionString).GetDatabase("ChineseAdministrativeDivision");
            Seed();
        }

        public void Seed()
        {
            BsonClassMap.RegisterClassMap<AdministrativeDivisionCn>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(_ => _.Code);
            });

            DataSet.Indexes.CreateMany(new[]
            {
                new CreateIndexModel<AdministrativeDivisionCn>(Builders<AdministrativeDivisionCn>.IndexKeys.Ascending(_ => _.Code)),
                new CreateIndexModel<AdministrativeDivisionCn>(Builders<AdministrativeDivisionCn>.IndexKeys.Ascending(_ => _.Level)),
                new CreateIndexModel<AdministrativeDivisionCn>(Builders<AdministrativeDivisionCn>.IndexKeys.Ascending(_ => _.ChildrenLoaded)),
            });
        }

        public IMongoCollection<AdministrativeDivisionCn> DataSet => database.GetCollection<AdministrativeDivisionCn>(nameof(DataSet));
    }
}
