using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Persistence;

public static class DbContextExtensions
{
    /// <summary>
    /// Marque une entité comme modifiée, sauf si elle vient d'être ajoutée dans le même contexte.
    /// Appeler <c>DbSet.Update</c> sur une entité « Added » dont la clé est déjà renseignée la transforme
    /// en UPDATE d'une ligne inexistante (DbUpdateConcurrencyException, « Ce document a été modifié »).
    /// </summary>
    public static void MarquerModifie<T>(this DbContext db, T entite) where T : class
    {
        if (db.Entry(entite).State == EntityState.Added) return;
        db.Update(entite);
    }
}
