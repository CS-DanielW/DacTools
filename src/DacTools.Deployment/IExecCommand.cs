// Copyright (c) 2020 DrBarnabus

// Copyright (c) 2019 DrBarnabus

// Copyright (c) 2019 DrBarnabus

using System.Threading;
using System.Threading.Tasks;

namespace DacTools.Deployment
{
    public interface IExecCommand
    {
        Task Execute(CancellationToken cancellationToken);
    }
}
