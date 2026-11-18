using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;

namespace VetGuard.Infrastructure.Services;

public sealed class QrCodeService : IQrCodeService
{
    public string GenerateSvgQrCode(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var svgQr = new SvgQRCode(data);
        return svgQr.GetGraphic(6, "#0f3d2e", "#ffffff", true);
    }

    public byte[] GeneratePngQrCode(string payload, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(data);
        return pngQr.GetGraphic(pixelsPerModule);
    }
}

public sealed class PdfReportService : IPdfReportService
{
    private readonly IQrCodeService _qrCodeService;

    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public PdfReportService(IQrCodeService qrCodeService)
    {
        _qrCodeService = qrCodeService;
    }

    public byte[] GenerateStallQrPassportPdf(AnimalDetailsDto details)
    {
        var a = details.Animal;
        var nowIkt = DateTime.UtcNow.AddHours(8);
        var qrBytes = _qrCodeService.GeneratePngQrCode(details.PassportUrl, 12);
        var isLocked = details.ActiveMilkLock is not null || details.ActiveMeatLock is not null || a.HealthStatus == HealthStatus.Quarantine;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10.5f).FontColor("#1a2421"));

                page.Header().Column(col =>
                {
                    col.Item().BorderBottom(2).BorderColor("#155e49").PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("ФГБОУ ВО Иркутский ГАУ им. А.А. Ежевского • Ветеринарный факультет")
                                .FontSize(9.5f).SemiBold().FontColor("#155e49");
                            left.Item().Text("СТОЙЛОВАЯ КАРТОЧКА И ВЕТЕРИНАРНЫЙ QR-ПАСПОРТ ЖИВОТНОГО")
                                .FontSize(15).Bold().FontColor("#0f2e24");
                            left.Item().Text($"Учебно-опытное фермерское хозяйство «Молодёжное» • Сформировано: {nowIkt:dd.MM.yyyy HH:mm} (ИКТ)")
                                .FontSize(8.5f).FontColor("#5c6f68");
                        });

                        row.ConstantItem(115).AlignRight().Border(1).BorderColor(isLocked ? "#dc2626" : "#15803d")
                            .Background(isLocked ? "#fef2f2" : "#f0fdf4").Padding(6).Column(badge =>
                            {
                                badge.Item().AlignCenter().Text(isLocked ? "КАРАНТИН!" : "ДОПУСК ОТКРЫТ")
                                    .FontSize(9.5f).Bold().FontColor(isLocked ? "#b91c1c" : "#15803d");
                                badge.Item().AlignCenter().Text(a.HealthStatus.ToDisplayName())
                                    .FontSize(8).FontColor("#374151");
                            });
                    });
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);

                    if (isLocked)
                    {
                        var lockText = details.ActiveMilkLock is not null
                            ? $"АКТИВЕН КАРАНТИННЫЙ ЗАМОК ПО МОЛОКУ! Препарат: «{details.ActiveMilkLock.DrugNameSnapshot}» (серия {details.ActiveMilkLock.DrugBatchSeriesSnapshot}). Полное выведение: {details.ActiveMilkLock.MilkWithdrawalEndsAtUtc.AddHours(8):dd.MM.yyyy HH:mm} (ИКТ). Сдача в общий танк ЗАПРЕЩЕНА!"
                            : details.ActiveMeatLock is not null
                                ? $"АКТИВЕН КАРАНТИН ПО МЯСУ! Препарат: «{details.ActiveMeatLock.DrugNameSnapshot}». Выведение до {details.ActiveMeatLock.MeatWithdrawalEndsAtUtc.AddHours(8):dd.MM.yyyy}."
                                : $"КАРАНТИННЫЙ ИЗОЛЯТОР: {a.QuarantineReason}";

                        col.Item().Border(1.5f).BorderColor("#dc2626").Background("#fef2f2").Padding(10)
                            .Text(lockText).FontSize(10).Bold().FontColor("#991b1b");
                    }

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor("#cbd5e1").Padding(12).Column(info =>
                        {
                            info.Spacing(5);
                            info.Item().Text($"{a.Name}  [{a.TagNumber}]").FontSize(16).Bold().FontColor("#0f2e24");
                            info.Item().Text($"RFID-транспондер ISO: {a.RfidChip ?? "Не чипировано"}");
                            info.Item().Text($"Вид / Порода: {a.Species.ToDisplayName()} • {a.Breed}");
                            info.Item().Text($"Пол: {a.Sex.ToDisplayName()}");
                            info.Item().Text($"Дата рождения: {a.BirthDate:dd.MM.yyyy} ({a.GetFormattedAge(DateTime.UtcNow)})");
                            info.Item().Text($"Размещение: {a.BarnName} — {a.SectionCode}");
                            info.Item().Text($"Генетическая линия: {a.LineageCode ?? "Стандарт стада"}");
                            info.Item().Text($"Живая масса: {a.CurrentWeightKg:0.0} кг" +
                                             (a.AverageDailyMilkKg.HasValue ? $" • Среднесуточный удой: {a.AverageDailyMilkKg:0.0} л/сут" : string.Empty));
                        });

                        row.ConstantItem(14).Text(string.Empty);

                        row.ConstantItem(165).Border(1).BorderColor("#155e49").Padding(8).Column(qrCol =>
                        {
                            qrCol.Item().AlignCenter().Text("СКАН ДЛЯ ВЕТВРАЧА").FontSize(8.5f).Bold().FontColor("#155e49");
                            qrCol.Item().PaddingVertical(4).AlignCenter().Width(125).Height(125).Image(qrBytes).FitArea();
                            qrCol.Item().AlignCenter().Text(a.TagNumber).FontSize(8.5f).Bold();
                            qrCol.Item().AlignCenter().Text("Мобильный осмотр у стойла").FontSize(7.5f).FontColor("#64748b");
                        });
                    });

                    col.Item().Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(10).Column(ped =>
                    {
                        ped.Spacing(4);
                        ped.Item().Text("Происхождение (Родители I ряда родословной)").FontSize(11).Bold().FontColor("#155e49");
                        ped.Item().Text($"Отец (Sire ♂): {(a.Sire != null ? $"{a.Sire.Name} [{a.Sire.TagNumber}] — {a.Sire.Breed}" : "Нет данных в базе")}");
                        ped.Item().Text($"Мать (Dam ♀): {(a.Dam != null ? $"{a.Dam.Name} [{a.Dam.TagNumber}] — {a.Dam.Breed}" : "Нет данных в базе")}");
                    });

                    col.Item().Text("Последние лечебно-профилактические обработки (Журнал ЛПМ)").FontSize(11).Bold().FontColor("#0f2e24");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80);
                            columns.RelativeColumn(1.4f);
                            columns.ConstantColumn(65);
                            columns.RelativeColumn(1.3f);
                            columns.ConstantColumn(95);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "Дата");
                            HeaderCell(header.Cell(), "Препарат / Серия");
                            HeaderCell(header.Cell(), "Доза");
                            HeaderCell(header.Cell(), "Диагноз");
                            HeaderCell(header.Cell(), "Выведение (мол.)");
                        });

                        var treatments = details.Treatments.Take(5).ToList();
                        if (treatments.Count == 0)
                        {
                            table.Cell().ColumnSpan(5).BorderBottom(1).BorderColor("#e2e8f0").Padding(6)
                                .Text("Записей о медикаментозных обработках антибиотиками нет.").FontSize(9).Italic();
                        }
                        else
                        {
                            foreach (var tr in treatments)
                            {
                                BodyCell(table.Cell(), tr.AdministeredAtUtc.AddHours(8).ToString("dd.MM.yy HH:mm"));
                                BodyCell(table.Cell(), $"{tr.DrugNameSnapshot} ({tr.DrugBatchSeriesSnapshot})");
                                BodyCell(table.Cell(), $"{tr.DosageAmount:0.#} {tr.DosageUnit}");
                                BodyCell(table.Cell(), tr.Diagnosis);
                                BodyCell(table.Cell(), tr.MilkWithdrawalHoursApplied > 0
                                    ? tr.MilkWithdrawalEndsAtUtc.AddHours(8).ToString("dd.MM.yy HH:mm")
                                    : "Без огранич.");
                            }
                        }
                    });
                });

                page.Footer().BorderTop(1).BorderColor("#cbd5e1").PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("АИС «VetGuard» ИрГАУ • Неизменяемый ветеринарный контроль (SQL Temporal Audit)").FontSize(8).FontColor("#64748b");
                    row.ConstantItem(180).AlignRight().Text("Подпись ветврача: __________________").FontSize(8.5f);
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateVeterinaryExtractPdf(AnimalDetailsDto details)
    {
        var a = details.Animal;
        var nowIkt = DateTime.UtcNow.AddHours(8);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor("#1e293b"));

                page.Header().BorderBottom(2).BorderColor("#0f3d2e").PaddingBottom(8).Column(col =>
                {
                    col.Item().Text("ВЕТЕРИНАРНАЯ СЛУЖБА УЧХОЗА ФГБОУ ВО ИРКУТСКИЙ ГАУ").FontSize(9.5f).Bold().FontColor("#155e49");
                    col.Item().Text($"ОФИЦИАЛЬНАЯ ВЕТЕРИНАРНАЯ ВЫПИСКА ИЗ ИСТОРИИ БОЛЕЗНИ И ОБРАБОТОК: {a.Name} [{a.TagNumber}]")
                        .FontSize(13).Bold();
                    col.Item().Text($"Дата выдачи выписки: {nowIkt:dd.MM.yyyy HH:mm} (ИКТ) • Статус здоровья: {a.HealthStatus.ToDisplayName()}")
                        .FontSize(9).FontColor("#475569");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(9).Column(card =>
                    {
                        card.Spacing(3);
                        card.Item().Text($"Инвентарный номер бирки: {a.TagNumber} • RFID: {a.RfidChip ?? "—"} • Порода: {a.Breed}");
                        card.Item().Text($"Пол: {a.Sex.ToDisplayName()} • Дата рождения: {a.BirthDate:dd.MM.yyyy} • Живая масса: {a.CurrentWeightKg:0.0} кг");
                        card.Item().Text($"Место содержания: {a.BarnName}, {a.SectionCode}");
                    });

                    col.Item().Text("1. Полный реестр лечебно-профилактических обработок (Temporal Audit)").FontSize(11).Bold().FontColor("#0f3d2e");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(75);
                            c.RelativeColumn(1.4f);
                            c.ConstantColumn(55);
                            c.RelativeColumn(1.3f);
                            c.ConstantColumn(80);
                            c.ConstantColumn(90);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Дата/Время");
                            HeaderCell(h.Cell(), "Препарат / Серия");
                            HeaderCell(h.Cell(), "Доза");
                            HeaderCell(h.Cell(), "Диагноз");
                            HeaderCell(h.Cell(), "Молоко до");
                            HeaderCell(h.Cell(), "Ветврач");
                        });

                        if (details.Treatments.Count == 0)
                        {
                            table.Cell().ColumnSpan(6).Padding(6).Text("Лечебные обработки не проводились.").Italic();
                        }
                        else
                        {
                            foreach (var t in details.Treatments)
                            {
                                BodyCell(table.Cell(), t.AdministeredAtUtc.AddHours(8).ToString("dd.MM.yy HH:mm"));
                                BodyCell(table.Cell(), $"{t.DrugNameSnapshot} ({t.DrugBatchSeriesSnapshot})");
                                BodyCell(table.Cell(), $"{t.DosageAmount:0.#} {t.DosageUnit}");
                                BodyCell(table.Cell(), t.Diagnosis);
                                BodyCell(table.Cell(), t.MilkWithdrawalHoursApplied > 0 ? t.MilkWithdrawalEndsAtUtc.AddHours(8).ToString("dd.MM.yy HH:mm") : "0 ч");
                                BodyCell(table.Cell(), t.VeterinarianName);
                            }
                        }
                    });

                    col.Item().Text("2. Противоэпизоотические вакцинации и диагностические исследования").FontSize(11).Bold().FontColor("#0f3d2e");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(75);
                            c.RelativeColumn(1.6f);
                            c.RelativeColumn(1.3f);
                            c.ConstantColumn(80);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Дата акта");
                            HeaderCell(h.Cell(), "Мероприятие / Биопрепарат");
                            HeaderCell(h.Cell(), "Результат");
                            HeaderCell(h.Cell(), "След. срок");
                        });

                        if (details.EpizooticHistory.Count == 0)
                        {
                            table.Cell().ColumnSpan(4).Padding(6).Text("Записей об эпизоотических мероприятиях нет.").Italic();
                        }
                        else
                        {
                            foreach (var e in details.EpizooticHistory)
                            {
                                var act = e.EpizooticAct;
                                BodyCell(table.Cell(), act != null ? act.ExecutedAtUtc.AddHours(8).ToString("dd.MM.yyyy") : "—");
                                BodyCell(table.Cell(), $"{act?.EpizooticRule?.Title ?? "Обработка"} ({act?.DrugNameSnapshot})");
                                BodyCell(table.Cell(), e.ResultText);
                                BodyCell(table.Cell(), e.NextDueAtUtc.ToString("dd.MM.yyyy"));
                            }
                        }
                    });

                    col.Item().Text("3. Клинические осмотры по QR-паспорту").FontSize(11).Bold().FontColor("#0f3d2e");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.ConstantColumn(60);
                            c.RelativeColumn(2f);
                            c.ConstantColumn(110);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Дата");
                            HeaderCell(h.Cell(), "Темп. °C");
                            HeaderCell(h.Cell(), "Клиническое заключение");
                            HeaderCell(h.Cell(), "Специалист");
                        });

                        if (details.Inspections.Count == 0)
                        {
                            table.Cell().ColumnSpan(4).Padding(6).Text("Осмотры не зарегистрированы.").Italic();
                        }
                        else
                        {
                            foreach (var ins in details.Inspections.Take(8))
                            {
                                BodyCell(table.Cell(), ins.InspectedAtUtc.AddHours(8).ToString("dd.MM.yy HH:mm"));
                                BodyCell(table.Cell(), $"{ins.BodyTemperatureC:0.0} °C");
                                BodyCell(table.Cell(), ins.Summary);
                                BodyCell(table.Cell(), ins.InspectorName);
                            }
                        }
                    });
                });

                page.Footer().BorderTop(1).BorderColor("#cbd5e1").PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("Документ сформирован автоматически АИС «VetGuard» ИрГАУ").FontSize(8).FontColor("#64748b");
                    row.ConstantItem(210).AlignRight().Text("Главный ветврач: _________________ М.П.").FontSize(8.5f);
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateBatchVetCertificatePdf(ProductBatch batch)
    {
        var nowIkt = DateTime.UtcNow.AddHours(8);
        var unit = batch.BatchType == ProductBatchType.RawMilk ? "л" : "кг";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor("#1e293b"));

                page.Header().BorderBottom(2).BorderColor("#155e49").PaddingBottom(8).Column(col =>
                {
                    col.Item().Text("ВЕТЕРИНАРНО-САНИТАРНОЕ УДОСТОВЕРЕНИЕ ДОПУСКА ПАРТИИ ПРОДУКЦИИ").FontSize(13).Bold().FontColor("#0f3d2e");
                    col.Item().Text($"Партия № {batch.BatchNumber} • Вид продукции: {batch.BatchType.ToDisplayName()} • Статус: {batch.Status.ToDisplayName()}")
                        .FontSize(9.5f).FontColor("#334155");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Border(1).BorderColor("#16a34a").Background("#f0fdf4").Padding(10)
                        .Text("КАРАНТИННЫЙ ЗАМОК ПРОЙДЕН: Ни одно животное в составе партии не имеет активных сроков выведения антибиотиков/ветпрепаратов и не числится в карантинном изоляторе.")
                        .FontSize(9.5f).Bold().FontColor("#166534");

                    col.Item().Border(1).BorderColor("#cbd5e1").Padding(10).Column(info =>
                    {
                        info.Spacing(4);
                        info.Item().Text($"Получатель партии: {batch.Destination}").SemiBold();
                        info.Item().Text($"Суммарный объем / масса нетто: {batch.TotalVolumeLitersOrKg:0.00} {unit}");
                        if (batch.BatchType == ProductBatchType.RawMilk)
                        {
                            info.Item().Text($"Физико-химические показатели: МДЖ {batch.FatPercent ?? 3.9m:0.00}% • МДБ {batch.ProteinPercent ?? 3.3m:0.00}% • Соматические клетки: {batch.SomaticCellsThousands ?? 140} тыс./см³");
                        }
                        info.Item().Text($"Сформировал оператор: {batch.CreatedByOperator} ({batch.CreatedAtUtc.AddHours(8):dd.MM.yyyy HH:mm} ИКТ)");
                        info.Item().Text($"Ветеринарный допуск: {batch.ApprovedByVeterinarian ?? "Ожидает подписи ветврача"}" +
                                         (batch.ApprovedAtUtc.HasValue ? $" ({batch.ApprovedAtUtc.Value.AddHours(8):dd.MM.yyyy HH:mm} ИКТ)" : string.Empty));
                    });

                    col.Item().Text("Реестр животных-поставщиков партии").FontSize(11).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(35);
                            c.ConstantColumn(125);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1.3f);
                            c.ConstantColumn(90);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "№");
                            HeaderCell(h.Cell(), "Бирка животного");
                            HeaderCell(h.Cell(), "Кличка / Порода");
                            HeaderCell(h.Cell(), "Корпус");
                            HeaderCell(h.Cell(), $"Объем ({unit})");
                        });

                        var idx = 1;
                        foreach (var item in batch.Items)
                        {
                            BodyCell(table.Cell(), (idx++).ToString());
                            BodyCell(table.Cell(), item.Animal?.TagNumber ?? "—");
                            BodyCell(table.Cell(), $"{item.Animal?.Name} ({item.Animal?.Breed})");
                            BodyCell(table.Cell(), item.Animal?.BarnName ?? "—");
                            BodyCell(table.Cell(), $"{item.QuantityLitersOrKg:0.00}");
                        }
                    });
                });

                page.Footer().BorderTop(1).BorderColor("#cbd5e1").PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text($"Выгружено: {nowIkt:dd.MM.yyyy HH:mm} • АИС «VetGuard» ИрГАУ").FontSize(8).FontColor("#64748b");
                    row.ConstantItem(220).AlignRight().Text("Ветсанэксперт: ____________________").FontSize(8.5f);
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateEpizooticActPdf(EpizooticAct act)
    {
        var execIkt = act.ExecutedAtUtc.AddHours(8);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor("#1e293b"));

                page.Header().BorderBottom(2).BorderColor("#0f3d2e").PaddingBottom(8).Column(col =>
                {
                    col.Item().Text("УЧЕБНО-ОПЫТНОЕ ХОЗЯЙСТВО ФГБОУ ВО ИРКУТСКИЙ ГАУ").FontSize(9.5f).Bold().FontColor("#155e49");
                    col.Item().Text($"АКТ ПРОТИВОЭПИЗООТИЧЕСКОЙ ВЕТЕРИНАРНОЙ ОБРАБОТКИ № {act.ActNumber}")
                        .FontSize(13.5f).Bold();
                    col.Item().Text($"Дата составления: {execIkt:dd.MM.yyyy HH:mm} (ИКТ) • Подразделение: {act.TargetBarn}")
                        .FontSize(9).FontColor("#475569");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(10).Column(summary =>
                    {
                        summary.Spacing(4);
                        summary.Item().Text($"Наименование мероприятия: {act.EpizooticRule?.Title ?? "Плановая ветеринарная обработка"}").Bold();
                        summary.Item().Text($"Нормативное основание: {act.EpizooticRule?.RegulatoryBasis ?? "Ветеринарные правила МСХ РФ"}");
                        summary.Item().Text($"Примененный биопрепарат / диагностикум: «{act.DrugNameSnapshot}» • Заводская серия: {act.DrugSeriesSnapshot}");
                        summary.Item().Text($"Охвачено поголовья: {act.Items.Count} гол. • Доза на 1 голову: {act.DosePerAnimal:0.##} • Израсходовано всего: {act.TotalDosesUsed:0.##}");
                        summary.Item().Text($"Комиссия: Ветврач — {act.ExecutedByVeterinarian}; Присутствовал — {act.WitnessZootechnician}");
                        if (!string.IsNullOrWhiteSpace(act.Notes))
                        {
                            summary.Item().Text($"Примечание: {act.Notes}");
                        }
                    });

                    col.Item().Text("Поименная опись обработанных животных").FontSize(11).Bold().FontColor("#0f3d2e");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(32);
                            c.ConstantColumn(120);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.6f);
                            c.ConstantColumn(80);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "№");
                            HeaderCell(h.Cell(), "Ушная бирка");
                            HeaderCell(h.Cell(), "Кличка / Порода");
                            HeaderCell(h.Cell(), "Результат / Реакция");
                            HeaderCell(h.Cell(), "След. дата");
                        });

                        var index = 1;
                        foreach (var item in act.Items)
                        {
                            BodyCell(table.Cell(), (index++).ToString());
                            BodyCell(table.Cell(), item.Animal?.TagNumber ?? "—");
                            BodyCell(table.Cell(), $"{item.Animal?.Name} ({item.Animal?.Breed})");
                            BodyCell(table.Cell(), item.ResultText);
                            BodyCell(table.Cell(), item.NextDueAtUtc.ToString("dd.MM.yyyy"));
                        }
                    });
                });

                page.Footer().BorderTop(1).BorderColor("#cbd5e1").PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Text($"Ветврач: {act.ExecutedByVeterinarian} _______________").FontSize(9);
                    row.RelativeItem().AlignRight().Text($"Зоотехник: {act.WitnessZootechnician} _______________").FontSize(9);
                });
            });
        }).GeneratePdf();
    }

    private static void HeaderCell(IContainer container, string title)
    {
        container
            .Background("#0f3d2e")
            .PaddingVertical(5)
            .PaddingHorizontal(6)
            .Text(title)
            .FontSize(8.5f)
            .Bold()
            .FontColor("#ffffff");
    }

    private static void BodyCell(IContainer container, string text)
    {
        container
            .BorderBottom(1)
            .BorderColor("#e2e8f0")
            .PaddingVertical(4.5f)
            .PaddingHorizontal(6)
            .Text(text)
            .FontSize(8.5f);
    }
}
