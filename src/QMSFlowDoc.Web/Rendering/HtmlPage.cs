using System.Net;
using System.Text;
using QMSFlowDoc.Web.Services;

namespace QMSFlowDoc.Web.Rendering;

public static class HtmlPage
{
    public static string Login(string? error = null) => Layout("Acceso", $$"""
        <section class="login-card">
            <h1>QMSFlowDoc v3</h1>
            <p class="muted">Portal web centralizado para la red local.</p>
            {{(string.IsNullOrWhiteSpace(error) ? string.Empty : $"<div class=\"alert\">{Encode(error)}</div>")}}
            <form method="post" action="/login" class="stack">
                <label>Usuario<input name="username" autocomplete="username" required autofocus /></label>
                <label>Contraseña<input name="password" type="password" autocomplete="current-password" required /></label>
                <button type="submit">Entrar</button>
            </form>
        </section>
        """, isAuthenticated: false);

    public static string Dashboard(DashboardSummary summary, string userName) => Layout("Panel", $$"""
        <header class="hero">
            <div>
                <p class="eyebrow">Servidor local</p>
                <h1>QMSFlowDoc v3</h1>
                <p>Base de datos y gestor documental centralizados en este servidor. Acceso desde cualquier equipo de la LAN mediante navegador.</p>
            </div>
            <div class="user-box">Sesión: <strong>{{Encode(userName)}}</strong></div>
        </header>
        <section class="grid cards">
            {{Metric("Documentos", summary.Documents)}}
            {{Metric("Aprobados", summary.ApprovedDocuments)}}
            {{Metric("Equipos", summary.Equipment)}}
            {{Metric("NC abiertas", summary.OpenNonconformities)}}
            {{Metric("Revisiones vencidas", summary.PendingReviews)}}
        </section>
        <section class="panel">
            <h2>Accesos rápidos</h2>
            <div class="actions">
                <a class="button" href="/documents">Gestión documental</a>
                <a class="button secondary" href="/health">Estado del servidor</a>
            </div>
        </section>
        """);

    public static string Documents(IReadOnlyList<DocumentListItem> documents, string? message = null) => Layout("Documentos", $$"""
        <section class="panel">
            <div class="section-title">
                <div>
                    <p class="eyebrow">Gestor documental</p>
                    <h1>Documentos</h1>
                </div>
                <a class="button secondary" href="/">Volver</a>
            </div>
            {{(string.IsNullOrWhiteSpace(message) ? string.Empty : $"<div class=\"success\">{Encode(message)}</div>")}}
            <form class="upload" method="post" action="/documents" enctype="multipart/form-data">
                <h2>Nuevo borrador</h2>
                <div class="grid form-grid">
                    <label>Código<input name="docCode" required /></label>
                    <label>Título<input name="title" required /></label>
                    <label>Área<input name="area" /></label>
                    <label>Proceso<input name="process" /></label>
                </div>
                <label>Archivo<input name="file" type="file" required /></label>
                <button type="submit">Subir al repositorio central</button>
            </form>
            <div class="table-wrap">
                <table>
                    <thead><tr><th>Código</th><th>Título</th><th>Estado</th><th>Versión</th><th>Área</th><th>Actualizado</th><th></th></tr></thead>
                    <tbody>
                        {{Rows(documents)}}
                    </tbody>
                </table>
            </div>
        </section>
        """);

    public static string Error(string title, string detail) => Layout(title, $$"""
        <section class="panel">
            <h1>{{Encode(title)}}</h1>
            <p>{{Encode(detail)}}</p>
            <a class="button" href="/">Volver</a>
        </section>
        """);

    private static string Layout(string title, string body, bool isAuthenticated = true) => $$"""
        <!doctype html>
        <html lang="es">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>{{Encode(title)}} · QMSFlowDoc v3</title>
            <style>
                :root { color-scheme: light; --bg:#eef4f8; --panel:#fff; --primary:#0f5b78; --primary-dark:#0a4055; --text:#18252d; --muted:#62717a; --line:#d8e2e8; --danger:#a23a3a; --ok:#2f7d52; }
                * { box-sizing: border-box; } body { margin:0; font-family: Segoe UI, Roboto, Arial, sans-serif; background:var(--bg); color:var(--text); }
                nav { display:flex; justify-content:space-between; align-items:center; padding:1rem 1.5rem; background:var(--primary-dark); color:#fff; }
                nav a { color:#fff; text-decoration:none; margin-left:1rem; } main { max-width:1180px; margin:0 auto; padding:2rem 1rem 4rem; }
                h1,h2 { margin:.2rem 0 .8rem; } .hero, .panel, .login-card { background:var(--panel); border:1px solid var(--line); border-radius:18px; padding:1.5rem; box-shadow:0 10px 24px rgba(10,64,85,.08); }
                .hero { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; margin-bottom:1rem; } .eyebrow { color:var(--primary); font-weight:700; text-transform:uppercase; letter-spacing:.08em; font-size:.78rem; margin:0; }
                .muted { color:var(--muted); } .grid { display:grid; gap:1rem; } .cards { grid-template-columns:repeat(auto-fit,minmax(160px,1fr)); margin-bottom:1rem; }
                .metric { background:var(--panel); border:1px solid var(--line); border-radius:16px; padding:1rem; } .metric strong { display:block; font-size:2rem; color:var(--primary-dark); }
                .button, button { display:inline-block; border:0; border-radius:10px; background:var(--primary); color:#fff; padding:.72rem 1rem; text-decoration:none; cursor:pointer; font-weight:650; }
                .button.secondary { background:#e5eef3; color:var(--primary-dark); } .actions { display:flex; gap:.75rem; flex-wrap:wrap; }
                .login-card { max-width:430px; margin:8vh auto; } .stack { display:grid; gap:1rem; } label { display:grid; gap:.35rem; font-weight:650; }
                input { width:100%; padding:.7rem .8rem; border:1px solid var(--line); border-radius:10px; font:inherit; background:#fff; }
                .alert { border:1px solid #efcaca; background:#fff2f2; color:var(--danger); padding:.8rem; border-radius:12px; margin:.8rem 0; }
                .success { border:1px solid #bfdfcc; background:#eefaf3; color:var(--ok); padding:.8rem; border-radius:12px; margin:.8rem 0; }
                .section-title { display:flex; justify-content:space-between; gap:1rem; align-items:center; } .upload { border:1px dashed var(--line); border-radius:14px; padding:1rem; margin:1rem 0; background:#f8fbfd; }
                .form-grid { grid-template-columns:repeat(auto-fit,minmax(210px,1fr)); } .table-wrap { overflow:auto; } table { width:100%; border-collapse:collapse; background:#fff; }
                th,td { padding:.75rem; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; } th { color:var(--muted); font-size:.84rem; text-transform:uppercase; letter-spacing:.04em; }
                .user-box { background:#f2f7fa; border-radius:12px; padding:.7rem .9rem; white-space:nowrap; }
                @media (max-width:700px) { .hero, .section-title { flex-direction:column; align-items:stretch; } nav { align-items:flex-start; gap:.5rem; flex-direction:column; } }
            </style>
        </head>
        <body>
            {{(isAuthenticated ? "<nav><strong>QMSFlowDoc v3</strong><span><a href=\"/\">Panel</a><a href=\"/documents\">Documentos</a><a href=\"/logout\">Salir</a></span></nav>" : string.Empty)}}
            <main>{{body}}</main>
        </body>
        </html>
        """;

    private static string Metric(string label, int value) => $"<article class=\"metric\"><span>{Encode(label)}</span><strong>{value}</strong></article>";

    private static string Rows(IReadOnlyList<DocumentListItem> documents)
    {
        if (documents.Count == 0)
            return "<tr><td colspan=\"7\" class=\"muted\">Todavía no hay documentos registrados.</td></tr>";

        var sb = new StringBuilder();
        foreach (var doc in documents)
        {
            var download = doc.HasFile ? $"<a class=\"button secondary\" href=\"/documents/{doc.Id}/download\">Descargar</a>" : "";
            sb.Append($$"""
                <tr>
                    <td><strong>{{Encode(doc.DocCode)}}</strong></td>
                    <td>{{Encode(doc.Title)}}<br><span class="muted">{{Encode(doc.Process ?? string.Empty)}}</span></td>
                    <td>{{Encode(doc.Status)}}</td>
                    <td>{{Encode(doc.CurrentVersion ?? "-")}}</td>
                    <td>{{Encode(doc.Area ?? "-")}}</td>
                    <td>{{doc.UpdatedAt:yyyy-MM-dd HH:mm}}</td>
                    <td>{{download}}</td>
                </tr>
                """);
        }
        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
