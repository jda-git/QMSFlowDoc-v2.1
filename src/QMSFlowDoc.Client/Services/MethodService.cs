using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Client.Services;

public interface IMethodService
{
    Task<List<MethodDto>> GetMethodsAsync();
    Task<Method?> GetMethodByIdAsync(Guid id);
    Task<Method> CreateMethodAsync(CreateMethodRequest request);
    Task UpdateMethodAsync(UpdateMethodRequest request);
    Task<List<MethodAuthorizationDto>> GetAuthorizationsAsync(Guid methodId);
    Task AuthorizeUserAsync(AuthorizeMethodRequest request);
    Task RemoveAuthorizationAsync(Guid authorizationId);
}

public class MethodService : IMethodService
{
    private readonly LocalDocumentStore _localStore;

    public MethodService(LocalDocumentStore localStore)
    {
        _localStore = localStore;
    }

    public Task<List<MethodDto>> GetMethodsAsync() => _localStore.GetMethodsAsync();

    public Task<Method?> GetMethodByIdAsync(Guid id) => _localStore.GetMethodByIdAsync(id);

    public Task<Method> CreateMethodAsync(CreateMethodRequest request) => _localStore.CreateMethodAsync(request);

    public Task UpdateMethodAsync(UpdateMethodRequest request) => _localStore.UpdateMethodAsync(request);

    public Task<List<MethodAuthorizationDto>> GetAuthorizationsAsync(Guid methodId) => _localStore.GetMethodAuthorizationsAsync(methodId);

    public Task AuthorizeUserAsync(AuthorizeMethodRequest request) => _localStore.AuthorizeUserForMethodAsync(request);

    public Task RemoveAuthorizationAsync(Guid authorizationId) => _localStore.RemoveMethodAuthorizationAsync(authorizationId);
}
