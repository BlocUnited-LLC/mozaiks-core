using MongoDB.Driver;
using Plugins.API.Models;

namespace Plugins.API.Repository;

public class PluginRepository : IPluginRepository
{
    private readonly IMongoCollection<Plugin> _plugins;
    private readonly IMongoCollection<PluginInstallation> _installations;

    public PluginRepository(IMongoDatabase database)
    {
        _plugins = database.GetCollection<Plugin>("plugins");
        _installations = database.GetCollection<PluginInstallation>("plugin_installations");

        // Create indexes
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        // Plugin indexes
        _plugins.Indexes.CreateOne(new CreateIndexModel<Plugin>(
            Builders<Plugin>.IndexKeys.Ascending(p => p.Name),
            new CreateIndexOptions { Unique = true }));

        _plugins.Indexes.CreateOne(new CreateIndexModel<Plugin>(
            Builders<Plugin>.IndexKeys.Ascending(p => p.Category)));

        _plugins.Indexes.CreateOne(new CreateIndexModel<Plugin>(
            Builders<Plugin>.IndexKeys.Text(p => p.DisplayName)
                .Text(p => p.Description)
                .Text(p => p.Tags)));

        // Installation indexes
        _installations.Indexes.CreateOne(new CreateIndexModel<PluginInstallation>(
            Builders<PluginInstallation>.IndexKeys
                .Ascending(i => i.AppId)
                .Ascending(i => i.PluginName),
            new CreateIndexOptions { Unique = true }));
    }

    public async Task<List<Plugin>> SearchPluginsAsync(
        string? query, string? category, List<string>? tags,
        int skip, int take, string sortBy, bool descending)
    {
        var filterBuilder = Builders<Plugin>.Filter;
        var filters = new List<FilterDefinition<Plugin>>
        {
            filterBuilder.Eq(p => p.IsPublished, true)
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            filters.Add(filterBuilder.Or(
                filterBuilder.Regex(p => p.DisplayName, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                filterBuilder.Regex(p => p.Description, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                filterBuilder.Regex(p => p.Name, new MongoDB.Bson.BsonRegularExpression(query, "i"))
            ));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filters.Add(filterBuilder.Eq(p => p.Category, category));
        }

        if (tags?.Any() == true)
        {
            filters.Add(filterBuilder.AnyIn(p => p.Tags, tags));
        }

        var filter = filterBuilder.And(filters);

        var sortDefinition = sortBy.ToLower() switch
        {
            "name" => descending 
                ? Builders<Plugin>.Sort.Descending(p => p.DisplayName)
                : Builders<Plugin>.Sort.Ascending(p => p.DisplayName),
            "rating" => descending
                ? Builders<Plugin>.Sort.Descending(p => p.Rating)
                : Builders<Plugin>.Sort.Ascending(p => p.Rating),
            "createdat" => descending
                ? Builders<Plugin>.Sort.Descending(p => p.CreatedAt)
                : Builders<Plugin>.Sort.Ascending(p => p.CreatedAt),
            _ => descending
                ? Builders<Plugin>.Sort.Descending(p => p.InstallCount)
                : Builders<Plugin>.Sort.Ascending(p => p.InstallCount)
        };

        return await _plugins.Find(filter)
            .Sort(sortDefinition)
            .Skip(skip)
            .Limit(take)
            .ToListAsync();
    }

    public async Task<int> CountPluginsAsync(string? query, string? category, List<string>? tags)
    {
        var filterBuilder = Builders<Plugin>.Filter;
        var filters = new List<FilterDefinition<Plugin>>
        {
            filterBuilder.Eq(p => p.IsPublished, true)
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            filters.Add(filterBuilder.Or(
                filterBuilder.Regex(p => p.DisplayName, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                filterBuilder.Regex(p => p.Description, new MongoDB.Bson.BsonRegularExpression(query, "i"))
            ));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filters.Add(filterBuilder.Eq(p => p.Category, category));
        }

        if (tags?.Any() == true)
        {
            filters.Add(filterBuilder.AnyIn(p => p.Tags, tags));
        }

        return (int)await _plugins.CountDocumentsAsync(filterBuilder.And(filters));
    }

    public async Task<Plugin?> GetPluginByIdAsync(string id)
    {
        return await _plugins.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Plugin?> GetPluginByNameAsync(string name)
    {
        return await _plugins.Find(p => p.Name == name).FirstOrDefaultAsync();
    }

    public async Task<Plugin> CreatePluginAsync(Plugin plugin)
    {
        await _plugins.InsertOneAsync(plugin);
        return plugin;
    }

    public async Task<Plugin> UpdatePluginAsync(Plugin plugin)
    {
        plugin.UpdatedAt = DateTime.UtcNow;
        await _plugins.ReplaceOneAsync(p => p.Id == plugin.Id, plugin);
        return plugin;
    }

    public async Task DeletePluginAsync(string id)
    {
        await _plugins.DeleteOneAsync(p => p.Id == id);
    }

    public async Task IncrementInstallCountAsync(string pluginId)
    {
        var update = Builders<Plugin>.Update.Inc(p => p.InstallCount, 1);
        await _plugins.UpdateOneAsync(p => p.Id == pluginId, update);
    }

    // Installation operations
    public async Task<List<PluginInstallation>> GetAppInstallationsAsync(string appId)
    {
        return await _installations.Find(i => i.AppId == appId).ToListAsync();
    }

    public async Task<PluginInstallation?> GetInstallationAsync(string appId, string pluginName)
    {
        return await _installations.Find(i => i.AppId == appId && i.PluginName == pluginName)
            .FirstOrDefaultAsync();
    }

    public async Task<PluginInstallation> CreateInstallationAsync(PluginInstallation installation)
    {
        await _installations.InsertOneAsync(installation);
        return installation;
    }

    public async Task<PluginInstallation> UpdateInstallationAsync(PluginInstallation installation)
    {
        installation.UpdatedAt = DateTime.UtcNow;
        await _installations.ReplaceOneAsync(i => i.Id == installation.Id, installation);
        return installation;
    }

    public async Task DeleteInstallationAsync(string installationId)
    {
        await _installations.DeleteOneAsync(i => i.Id == installationId);
    }
}
