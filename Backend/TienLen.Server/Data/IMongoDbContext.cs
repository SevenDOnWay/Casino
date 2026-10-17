using System;

namespace TienLen.Server.Data {
    /// <summary>
    /// Legacy compatibility interface. Use IDataBaseContext instead.
    /// </summary>
    [Obsolete("Use IDataBaseContext or IDatabaseContext instead.")]
    public interface IMongoDbContext : IDataBaseContext { }
}
