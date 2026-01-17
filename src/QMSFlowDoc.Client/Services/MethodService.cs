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

    // Versioning & Validation
    Task<List<MethodVersionDto>> GetVersionsAsync(Guid methodId);
    Task CreateVersionAsync(CreateMethodVersionRequest request);
    Task ApproveVersionAsync(Guid versionId, string approvedBy);
    Task<List<MethodValidationDto>> GetValidationsAsync(Guid versionId);
    Task AddValidationAsync(MethodValidationDto validation);
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

    // Versioning Implementation
    public Task<List<MethodVersionDto>> GetVersionsAsync(Guid methodId) => _localStore.GetMethodVersionsAsync(methodId);

    public Task CreateVersionAsync(CreateMethodVersionRequest request) => _localStore.CreateMethodVersionAsync(request);

    public Task ApproveVersionAsync(Guid versionId, string approvedBy) => _localStore.ApproveMethodVersionAsync(versionId, approvedBy);

    public Task<List<MethodValidationDto>> GetValidationsAsync(Guid versionId) => _localStore.GetMethodValidationsAsync(versionId);

    public Task AddValidationAsync(MethodValidationDto validation) => _localStore.AddMethodValidationAsync(validation);
}
