using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FabTrace.Data;

internal static class SqlErrors
{
    // SQL Server error numbers: 2601 = duplicate row in a unique index,
    // 2627 = unique or primary key constraint violated.
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}