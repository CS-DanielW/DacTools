// Copyright (c) 2020 DrBarnabus

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DacTools.Deployment.Core.DatabaseListGenerators
{
    public interface IWhitelistDatabaseListGenerator
    {
        Task<List<DatabaseInfo>> GetDatabaseInfoListAsync(IReadOnlyList<string> databaseNames = null, CancellationToken cancellationToken = default);
    }
}
