using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.Data.Sqlite;

namespace QMSFlowDoc.Client.Services;

public interface ISupplierService
{
    Task<List<SupplierListDto>> GetSuppliersAsync();
    Task<SupplierDetailDto?> GetSupplierByIdAsync(Guid id);
    Task<Guid> CreateSupplierAsync(CreateSupplierRequest request);
    Task<bool> UpdateSupplierAsync(SupplierDetailDto supplier);
    Task<bool> DeleteSupplierAsync(Guid id);
    Task<Guid> CreateEvaluationAsync(CreateSupplierEvaluationRequest request, Guid? evaluatorUserId);
    Task<List<SupplierEvaluationDto>> GetEvaluationsAsync(Guid supplierId);
    Task<int> GetSupplierIncidentCountAsync(Guid supplierId);
    Task UpdateExpiredEvaluationsAsync();
}

public class SupplierService : ISupplierService
{
    private readonly LocalDocumentStore _store;

    public SupplierService(LocalDocumentStore store)
    {
        _store = store;
    }

    public async Task<List<SupplierListDto>> GetSuppliersAsync()
    {
        return await _store.GetSuppliersAsync();
    }

    public async Task<SupplierDetailDto?> GetSupplierByIdAsync(Guid id)
    {
        return await _store.GetSupplierByIdAsync(id);
    }

    public async Task<Guid> CreateSupplierAsync(CreateSupplierRequest request)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ContactName = request.ContactName,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            Notes = request.Notes,
            Type = request.Type,
            QualityStatus = SupplierQualityStatus.PENDIENTE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _store.CreateSupplierAsync(supplier);
        return supplier.Id;
    }

    public async Task<bool> UpdateSupplierAsync(SupplierDetailDto supplier)
    {
        return await _store.UpdateSupplierAsync(supplier);
    }

    public async Task<bool> DeleteSupplierAsync(Guid id)
    {
        return await _store.DeleteSupplierAsync(id);
    }

    public async Task<Guid> CreateEvaluationAsync(CreateSupplierEvaluationRequest request, Guid? evaluatorUserId)
    {
        var evaluation = new SupplierEvaluation
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            EvaluationDate = request.EvaluationDate,
            EvaluatorUserId = evaluatorUserId,
            EvaluatedPeriod = request.EvaluatedPeriod,
            ScorePlazos = request.ScorePlazos,
            ScoreCalidad = request.ScoreCalidad,
            ScoreServicio = request.ScoreServicio,
            ScoreIncidencias = request.ScoreIncidencias,
            IsApproved = request.IsApproved,
            Observations = request.Observations,
            AttachmentPath = request.AttachmentPath,
            CreatedAt = DateTime.UtcNow
        };

        // Business Logic: Calculate status based on scores
        var average = evaluation.AverageScore;
        SupplierQualityStatus newStatus;
        
        if (!request.IsApproved || average < 3.0)
        {
            newStatus = SupplierQualityStatus.NO_APTO;
        }
        else if (average < 4.0)
        {
            newStatus = SupplierQualityStatus.EN_OBSERVACION;
        }
        else
        {
            newStatus = SupplierQualityStatus.APTO;
        }

        // Calculate next evaluation date (1 year from now for approved)
        DateTime? nextEvalDate = null;
        if (request.IsApproved)
        {
            nextEvalDate = request.EvaluationDate.AddYears(1);
        }

        await _store.CreateSupplierEvaluationAsync(evaluation, newStatus, nextEvalDate);
        return evaluation.Id;
    }

    public async Task<List<SupplierEvaluationDto>> GetEvaluationsAsync(Guid supplierId)
    {
        return await _store.GetSupplierEvaluationsAsync(supplierId);
    }

    public async Task<int> GetSupplierIncidentCountAsync(Guid supplierId)
    {
        // Count incidents with Origin containing supplier name or where supplier is referenced
        return await _store.GetSupplierIncidentCountAsync(supplierId);
    }

    public async Task UpdateExpiredEvaluationsAsync()
    {
        // Mark suppliers as EVALUACION_CADUCADA if NextEvaluationDate < today
        await _store.UpdateExpiredSupplierEvaluationsAsync();
    }
}
