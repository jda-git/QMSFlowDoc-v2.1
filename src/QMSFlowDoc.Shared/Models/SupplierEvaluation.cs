using System;

namespace QMSFlowDoc.Shared.Models;

/// <summary>
/// Supplier Evaluation for ISO 15189:2022 (Section 6.8)
/// </summary>
public class SupplierEvaluation
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateTime EvaluationDate { get; set; }
    public Guid? EvaluatorUserId { get; set; }
    public string EvaluatedPeriod { get; set; } = string.Empty; // e.g., "2024-2025"
    
    // Scores (1-5 scale)
    public int ScorePlazos { get; set; }      // Delivery times compliance
    public int ScoreCalidad { get; set; }     // Product/service quality
    public int ScoreServicio { get; set; }    // Technical support quality
    public int ScoreIncidencias { get; set; } // Incident handling (inverse: 5=no incidents)
    
    // Result
    public bool IsApproved { get; set; }
    public string? Observations { get; set; }
    public string? AttachmentPath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Calculated property for average score
    public double AverageScore => (ScorePlazos + ScoreCalidad + ScoreServicio + ScoreIncidencias) / 4.0;
}
