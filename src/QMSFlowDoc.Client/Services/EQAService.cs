using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Client.Services;

public interface IEQAService
{
    Task<List<EQAProgramDto>> GetProgramsAsync();
    Task<EQAProgram> CreateProgramAsync(CreateEQAProgramRequest request);
    Task UpdateProgramAsync(UpdateEQAProgramRequest request);
    Task<List<EQAResultDto>> GetResultsAsync(Guid programId);
    Task RegisterResultAsync(RegisterEQAResultRequest request);
    Task UpdateResultAsync(UpdateEQAResultRequest request);
}

public class EQAService : IEQAService
{
    private readonly LocalDocumentStore _localStore;

    public EQAService(LocalDocumentStore localStore)
    {
        _localStore = localStore;
    }

    public async Task<List<EQAProgramDto>> GetProgramsAsync()
    {
        return await _localStore.GetEQAProgramsAsync();
    }

    public async Task<EQAProgram> CreateProgramAsync(CreateEQAProgramRequest request)
    {
        return await _localStore.CreateEQAProgramAsync(request);
    }

    public async Task UpdateProgramAsync(UpdateEQAProgramRequest request)
    {
        await _localStore.UpdateEQAProgramAsync(request);
    }

    public async Task<List<EQAResultDto>> GetResultsAsync(Guid programId)
    {
        return await _localStore.GetEQAResultsAsync(programId);
    }

    public async Task RegisterResultAsync(RegisterEQAResultRequest request)
    {
        await _localStore.RegisterEQAResultAsync(request);
    }

    public async Task UpdateResultAsync(UpdateEQAResultRequest request)
    {
        await _localStore.UpdateEQAResultAsync(request);
    }
}
