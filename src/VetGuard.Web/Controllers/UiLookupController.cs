using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetGuard.Infrastructure.Persistence;

namespace VetGuard.Web.Controllers;

[Route("UiLookup")]
public sealed class UiLookupController : Controller
{
    private readonly VetGuardDbContext _db;

    public UiLookupController(VetGuardDbContext db)
    {
        _db = db;
    }

    public sealed record SearchEntry(string Category, string Title, string Subtitle, string Url);

    [HttpGet("SearchIndex")]
    public async Task<IActionResult> SearchIndex(CancellationToken ct)
    {
        var items = new List<SearchEntry>
        {
            new("Раздел", "Обзор и сводка ветслужбы", "Главная панель учхоза ИрГАУ, схема корпусов и таймеры карантина", "/"),
            new("Раздел", "Реестр поголовья и QR-паспорта", "Учетные карточки КРС, RFID, графики удоев и привесов", "/Animals"),
            new("Раздел", "Журнал ЛПМ (инъекции и обработки)", "Введение антибиотиков и расчет периода выведения", "/Treatments"),
            new("Раздел", "Контроль партий и Карантинный замок", "Формирование танков молока, блокировка карантинного сырья и ВСД", "/Batches"),
            new("Раздел", "Справочник ветпрепаратов и вакцин", "Сроки выведения (молоко/мясо) и остатки серий с RowVersion", "/Drugs"),
            new("Раздел", "Племенной радар и Анти-инбридинг (CTE)", "Родословное дерево до 6 колен и расчет коэффициента С. Райта", "/Breeding"),
            new("Раздел", "Противоэпизоотический календарь и Изолятор", "Групповые вакцинации в 2 клика, PDF-акты и боксы изоляции", "/Epizootics"),
            new("Аудит СУБД", "Неизменяемый аудит Temporal Tables", "История SYSTEM_VERSIONING = ON (FOR SYSTEM_TIME ALL)", "/Audit"),
            new("Профиль", "Быстрый демо-вход по ролям", "Главный ветврач, Зоотехник-селекционер, Оператор доильного зала", "/Account/Login")
        };

        try
        {
            var animals = await _db.Animals
                .AsNoTracking()
                .Where(a => !a.IsHistoricalPedigreeOnly)
                .OrderBy(a => a.TagNumber)
                .Take(30)
                .ToListAsync(ct);

            foreach (var a in animals)
            {
                items.Add(new SearchEntry(
                    "Животное",
                    $"{a.Name} [{a.TagNumber}]",
                    $"{a.Breed} · {a.BarnName} ({a.SectionCode})",
                    $"/Animals/Details/{a.Id}"));

                items.Add(new SearchEntry(
                    "QR-Осмотр",
                    $"Мобильный осмотр: {a.Name}",
                    $"Стойловый QR-терминал ветврача · {a.TagNumber}",
                    $"/Passport/{Uri.EscapeDataString(a.TagNumber)}"));
            }

            var drugs = await _db.VetDrugs
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Take(15)
                .ToListAsync(ct);

            foreach (var d in drugs)
            {
                items.Add(new SearchEntry(
                    "Препарат",
                    d.Name,
                    $"Серия {d.BatchSeries} · Молоко: {d.MilkWithdrawalHours} ч · Мясо: {d.MeatWithdrawalDays} сут",
                    "/Drugs"));
            }

            var batches = await _db.ProductBatches
                .AsNoTracking()
                .OrderByDescending(b => b.CreatedAtUtc)
                .Take(10)
                .ToListAsync(ct);

            foreach (var b in batches)
            {
                items.Add(new SearchEntry(
                    "Партия",
                    b.BatchNumber,
                    $"{b.Destination} · {b.TotalVolumeLitersOrKg:0.0} л/кг",
                    "/Batches"));
            }
        }
        catch
        {
            // При недоступности БД возвращаем базовые навигационные маршруты
        }

        return Json(items);
    }
}
