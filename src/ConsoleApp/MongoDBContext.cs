
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace ConsoleApp;

class MongoDBContext
{
    readonly IMongoClient _client;
    readonly IMongoDatabase _database;
    public MongoDBContext(MongoUrl url)
    {
        _client = new MongoClient(url);
        _database = _client.GetDatabase(url.DatabaseName);
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

    public IMongoCollection<AdministrativeDivisionCn> DataSet => _database.GetCollection<AdministrativeDivisionCn>(nameof(DataSet));
}
