namespace EHub.Domain.Enums;

public enum DataBankAuditAction
{
    ImportPreviewed,
    ImportCommitted,
    ImportRolledBack,
    Exported,
    TemplateCreated,
    TemplateUpdated,
    FieldUpdated,
    ColumnMapped,
    ColumnCreated
}
