using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;

namespace VetGuard.Infrastructure.Persistence;

public static class VetGuardDbSeeder
{
    public static async Task SeedAsync(VetGuardDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        if (await db.Animals.AnyAsync(ct))
        {
            return;
        }

        logger.LogInformation("Запуск первичного наполнения БД IrGau_VetGuardDb демонстрационными данными учхоза ИрГАУ...");
        var nowUtc = DateTime.UtcNow;

        // =========================================================================
        // 1. СПРАВОЧНИК ВЕТПРЕПАРАТОВ, АНТИБИОТИКОВ, ВАКЦИН И ДИАГНОСТИКУМОВ
        // =========================================================================
        var drugNitox = new VetDrug
        {
            Name = "Нитокс 200 (Окситетрациклин 20%)",
            ActiveSubstance = "Окситетрациклина дигидрат 200 мг/мл",
            Category = DrugCategory.Antibiotic,
            Manufacturer = "ООО «НИТА-ФАРМ» (г. Саратов)",
            BatchSeries = "NTX-0826-41",
            ExpirationDate = nowUtc.Date.AddMonths(18),
            MilkWithdrawalHours = 168, // 7 суток (168 часов) строгий карантин по молоку!
            MeatWithdrawalDays = 28,
            DefaultDosageText = "1 мл на 10 кг массы тела однократно глубоко в/м",
            Unit = "мл",
            StockDoses = 480m,
            IsPotent = true,
            StorageConditions = "Список Б. От +5 °C до +25 °C в защищенном от света месте"
        };

        var drugAmox = new VetDrug
        {
            Name = "Амоксициллин 150 LA",
            ActiveSubstance = "Амоксициллина тригидрат 150 мг/мл",
            Category = DrugCategory.Antibiotic,
            Manufacturer = "INVESA / Industrial Veterinaria",
            BatchSeries = "AMX-0526-19",
            ExpirationDate = nowUtc.Date.AddMonths(14),
            MilkWithdrawalHours = 96, // 4 суток (96 часов) по молоку!
            MeatWithdrawalDays = 21,
            DefaultDosageText = "1 мл на 10 кг живой массы в/м или п/к",
            Unit = "мл",
            StockDoses = 350m,
            IsPotent = true,
            StorageConditions = "От +5 °C до +20 °C, строго подотчетный антибиотик"
        };

        var drugEnro = new VetDrug
        {
            Name = "Энрофлон 10% инъекционный",
            ActiveSubstance = "Энрофлоксацин 100 мг/мл",
            Category = DrugCategory.Antibiotic,
            Manufacturer = "ВИК — Здоровье животных",
            BatchSeries = "ENR-0926-07",
            ExpirationDate = nowUtc.Date.AddMonths(16),
            MilkWithdrawalHours = 120, // 5 суток (120 часов)
            MeatWithdrawalDays = 14,
            DefaultDosageText = "2.5–5 мл на 100 кг массы тела 1 раз в сутки",
            Unit = "мл",
            StockDoses = 260m,
            IsPotent = true,
            StorageConditions = "Список Б. Строгий ветеринарный учет"
        };

        var drugIvermek = new VetDrug
        {
            Name = "Ивермек 1% стерильный",
            ActiveSubstance = "Ивермектин 10 мг/мл + Токоферол",
            Category = DrugCategory.Antiparasitic,
            Manufacturer = "ООО «НИТА-ФАРМ»",
            BatchSeries = "IVM-0426-88",
            ExpirationDate = nowUtc.Date.AddMonths(20),
            MilkWithdrawalHours = 672, // 28 суток для лактирующих (обычно в сухостой)
            MeatWithdrawalDays = 28,
            DefaultDosageText = "1 мл на 50 кг массы тела п/к",
            Unit = "мл",
            StockDoses = 300m,
            IsPotent = true,
            StorageConditions = "В закрытой упаковке при +2...+25 °C"
        };

        var drugKetoprofen = new VetDrug
        {
            Name = "Айнил 10% (Кетопрофен)",
            ActiveSubstance = "Кетопрофен 100 мг/мл",
            Category = DrugCategory.NSAID,
            Manufacturer = "LIVISTO",
            BatchSeries = "AIN-1026-12",
            ExpirationDate = nowUtc.Date.AddMonths(22),
            MilkWithdrawalHours = 0, // Кетопрофен без браковки молока!
            MeatWithdrawalDays = 4,
            DefaultDosageText = "3 мл на 100 кг живой массы в/м 1–3 дня",
            Unit = "мл",
            StockDoses = 190m,
            IsPotent = false,
            StorageConditions = "От +5 °C до +25 °C"
        };

        var vacAnthrax = new VetDrug
        {
            Name = "Вакцина против сибирской язвы животных (штамм 55-ВНИИВВиМ)",
            ActiveSubstance = "Живые споры бескапсульного штамма 55-ВНИИВВиМ",
            Category = DrugCategory.Vaccine,
            Manufacturer = "ФКП «Орловская биофабрика»",
            BatchSeries = "VAC-SYA-2026-03",
            ExpirationDate = nowUtc.Date.AddMonths(11),
            MilkWithdrawalHours = 0,
            MeatWithdrawalDays = 10,
            DefaultDosageText = "1.0 мл подкожно в область шеи",
            Unit = "доз",
            StockDoses = 180m,
            IsPotent = true,
            StorageConditions = "Холодовая цепь строго от +2 °C до +8 °C"
        };

        var diagTuberculin = new VetDrug
        {
            Name = "Туберкулин очищенный (ППД) для млекопитающих",
            ActiveSubstance = "Аллерген туберкулопротеин стандартный 2 мл (10 000 МЕ)",
            Category = DrugCategory.DiagnosticAllergen,
            Manufacturer = "ФКП «Курская биофабрика — БИОК»",
            BatchSeries = "PPD-2026-15",
            ExpirationDate = nowUtc.Date.AddMonths(10),
            MilkWithdrawalHours = 0,
            MeatWithdrawalDays = 0,
            DefaultDosageText = "0.2 мл внутрикожно безыгольным инъектором БИ-7",
            Unit = "доз",
            StockDoses = 250m,
            IsPotent = false,
            StorageConditions = "От +2 °C до +8 °C, не замораживать"
        };

        var diagBrucellosis = new VetDrug
        {
            Name = "Набор антигена бруцеллёзного единого для РА, РСК и РДСК",
            ActiveSubstance = "Инактивированная взвесь Brucella abortus штамм 19",
            Category = DrugCategory.DiagnosticAllergen,
            Manufacturer = "ФКП «Щёлковский биокомбинат»",
            BatchSeries = "BRC-2026-09",
            ExpirationDate = nowUtc.Date.AddMonths(9),
            MilkWithdrawalHours = 0,
            MeatWithdrawalDays = 0,
            DefaultDosageText = "Отбор проб сыворотки крови (1 проба/гол.)",
            Unit = "проб",
            StockDoses = 300m,
            IsPotent = false,
            StorageConditions = "От +2 °C до +8 °C"
        };

        var diagLeukosis = new VetDrug
        {
            Name = "Набор для серологической диагностики лейкоза КРС (РИД)",
            ActiveSubstance = "Гликопротеидный антиген ВЛКРС gp51",
            Category = DrugCategory.DiagnosticAllergen,
            Manufacturer = "ФКП «Курская биофабрика»",
            BatchSeries = "RID-2026-22",
            ExpirationDate = nowUtc.Date.AddMonths(12),
            MilkWithdrawalHours = 0,
            MeatWithdrawalDays = 0,
            DefaultDosageText = "Исследование сыворотки крови в РИД (1 проба/гол.)",
            Unit = "проб",
            StockDoses = 300m,
            IsPotent = false,
            StorageConditions = "От +2 °C до +8 °C"
        };

        db.VetDrugs.AddRange(
            drugNitox, drugAmox, drugEnro, drugIvermek, drugKetoprofen,
            vacAnthrax, diagTuberculin, diagBrucellosis, diagLeukosis);
        await db.SaveChangesAsync(ct);

        // =========================================================================
        // 2. МНОГОКОЛЕННОЕ ГЕНЕАЛОГИЧЕСКОЕ ДЕРЕВО (6 ПОКОЛЕНИЙ КРС ДЛЯ РЕКУРСИВНЫХ CTE)
        // =========================================================================
        Animal Archival(string tag, string name, AnimalSex sex, string breed, int birthYear, string lineage, Guid? sireId = null, Guid? damId = null)
        {
            return new Animal
            {
                TagNumber = tag,
                Name = name,
                Species = AnimalSpecies.Cattle,
                Breed = breed,
                Sex = sex,
                BirthDate = new DateTime(birthYear, 4, 15),
                SireId = sireId,
                DamId = damId,
                IsHistoricalPedigreeOnly = true,
                BarnName = "Племенная книга ИрГАУ (Архив)",
                SectionCode = "ГПК",
                LineageCode = lineage,
                HealthStatus = HealthStatus.Healthy,
                CurrentWeightKg = sex == AnimalSex.Male ? 940m : 620m
            };
        }

        // VI колено (основатели линий — 2009 г.)
        var g6Sire1 = Archival("SU-38-HIST-0101", "Атлант-Основатель", AnimalSex.Male, "Голштинская", 2009, "Вис Бэк Айдиал 1013415");
        var g6Dam1 = Archival("SU-38-HIST-0102", "Вега-Прайм", AnimalSex.Female, "Голштинская", 2009, "Вис Бэк Айдиал 1013415");
        var g6Sire2 = Archival("SU-38-HIST-0103", "Байкал-Колос", AnimalSex.Male, "Черно-пестрая", 2009, "Аннас Адема 30587");
        var g6Dam2 = Archival("SU-38-HIST-0104", "Звезда-Сибири", AnimalSex.Female, "Черно-пестрая", 2009, "Аннас Адема 30587");

        // V колено (2012 г.)
        var g5Sire1 = Archival("RU-38-HIST-0201", "Титан-Голд", AnimalSex.Male, "Голштинская", 2012, "Вис Бэк Айдиал 1013415", g6Sire1.Id, g6Dam1.Id);
        var g5Dam1 = Archival("RU-38-HIST-0202", "Нива-Люкс", AnimalSex.Female, "Голштинская", 2012, "Монтвик Чифтейн 95679");
        var g5Sire2 = Archival("RU-38-HIST-0203", "Иртыш-Могучий", AnimalSex.Male, "Черно-пестрая", 2012, "Аннас Адема 30587", g6Sire2.Id, g6Dam2.Id);
        var g5Dam2 = Archival("RU-38-HIST-0204", "Калина-Рекорд", AnimalSex.Female, "Черно-пестрая", 2012, "Посейдон 239");

        // IV колено (2015 г.)
        var g4Sire1 = Archival("RU-38-HIST-0301", "Маршал-Рекорд", AnimalSex.Male, "Голштинская", 2015, "Вис Бэк Айдиал 1013415", g5Sire1.Id, g5Dam1.Id);
        var g4Dam1 = Archival("RU-38-HIST-0302", "Лада-Сибирь", AnimalSex.Female, "Голштинская", 2015, "Рефлекшн Соверинг 198998");
        var g4Sire2 = Archival("RU-38-HIST-0303", "Урал-Гранит", AnimalSex.Male, "Черно-пестрая", 2015, "Аннас Адема 30587", g5Sire2.Id, g5Dam2.Id);
        var g4Dam2 = Archival("RU-38-HIST-0304", "Берёзка-Плюс", AnimalSex.Female, "Черно-пестрая", 2015, "Аннас Адема 30587");

        // III колено (прадеды и прабабки — 2017 г.)
        var g3Sultan = Archival("RU-38-HIST-0401", "Султан-Идеал", AnimalSex.Male, "Голштинская", 2017, "Вис Бэк Айдиал 1013415", g4Sire1.Id, g4Dam1.Id);
        var g3Malva = Archival("RU-38-HIST-0402", "Мальва-Элита", AnimalSex.Female, "Голштинская", 2017, "Пабст Говернер 882933");
        var g3Sayan = Archival("RU-38-HIST-0403", "Саян-Монолит", AnimalSex.Male, "Черно-пестрая", 2017, "Аннас Адема 30587", g4Sire2.Id, g4Dam2.Id);
        var g3Kama = Archival("RU-38-HIST-0404", "Кама-Звезда", AnimalSex.Female, "Черно-пестрая", 2017, "Аннас Адема 30587");
        var g3Viking = Archival("RU-38-HIST-0405", "Викинг-Север", AnimalSex.Male, "Голштинская", 2017, "Рефлекшн Соверинг 198998");
        var g3Silva = Archival("RU-38-HIST-0406", "Сильва-Гранд", AnimalSex.Female, "Голштинская", 2017, "Рефлекшн Соверинг 198998");

        // II колено (деды и бабки — 2019 г.)
        var g2Olimp = Archival("RU-38-HIST-0501", "Олимп-Фаворит", AnimalSex.Male, "Голштинская", 2019, "Вис Бэк Айдиал 1013415", g3Sultan.Id, g3Malva.Id);
        var g2Avrora = Archival("RU-38-HIST-0502", "Аврора-Плюс", AnimalSex.Female, "Голштинская", 2019, "Пабст Говернер 882933");
        var g2Vityaz = Archival("RU-38-HIST-0503", "Витязь-Ангара", AnimalSex.Male, "Черно-пестрая", 2019, "Аннас Адема 30587", g3Sayan.Id, g3Kama.Id);
        var g2Chayka = Archival("RU-38-HIST-0504", "Чайка-Рекорд", AnimalSex.Female, "Голштинская", 2019, "Рефлекшн Соверинг 198998", g3Viking.Id, g3Silva.Id);

        // I колено (родители основного стада и быков — 2021 г.)
        var g1Cezar = Archival("RU-38-HIST-0601", "Цезарь-Премиум", AnimalSex.Male, "Голштинская", 2021, "Вис Бэк Айдиал 1013415", g2Olimp.Id, g2Avrora.Id);
        var g1Dvina = Archival("RU-38-HIST-0602", "Двина-Славянка", AnimalSex.Female, "Голштинская", 2021, "Рефлекшн Соверинг 198998", g2Vityaz.Id, g2Chayka.Id);
        var g1Kedr = Archival("RU-38-HIST-0612", "Кедр-Элит", AnimalSex.Male, "Голштинская", 2020, "Вис Бэк Айдиал 1013415", g3Sultan.Id, null);
        var g1Baron = Archival("RU-38-HIST-0613", "Барон-Сибирь", AnimalSex.Male, "Голштинская", 2021, "Вис Бэк Айдиал 1013415", g2Olimp.Id, null);
        var g1Tayga = Archival("RU-38-HIST-0614", "Тайга-Плюс", AnimalSex.Female, "Черно-пестрая", 2021, "Аннас Адема 30587", g3Sayan.Id, null);
        var g1Nord = Archival("CA-00-HIST-0610", "Норд-Канада ET", AnimalSex.Male, "Голштинская", 2020, "Ситейшн Р 267150");
        var g1Snezhana = Archival("CA-00-HIST-0611", "Снежана-Альфа", AnimalSex.Female, "Голштинская", 2020, "Ситейшн Р 267150");

        db.Animals.AddRange(
            g6Sire1, g6Dam1, g6Sire2, g6Dam2,
            g5Sire1, g5Dam1, g5Sire2, g5Dam2,
            g4Sire1, g4Dam1, g4Sire2, g4Dam2,
            g3Sultan, g3Malva, g3Sayan, g3Kama, g3Viking, g3Silva,
            g2Olimp, g2Avrora, g2Vityaz, g2Chayka,
            g1Cezar, g1Dvina, g1Kedr, g1Baron, g1Tayga, g1Nord, g1Snezhana);
        await db.SaveChangesAsync(ct);

        // =========================================================================
        // 3. ДЕЙСТВУЮЩЕЕ ПРОИЗВОДСТВЕННОЕ И ПЛЕМЕННОЕ СТАДО УЧХОЗА ИрГАУ
        // =========================================================================

        // 4 быка-производителя племрепродуктора (дают все 4 градации риска инбридинга!)
        var bullAlmaz = new Animal
        {
            TagNumber = "RU-38-IRGAU-0011",
            RfidChip = "643098100038011",
            Name = "Алмаз-Сибирь",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Male,
            BirthDate = new DateTime(2022, 5, 10),
            SireId = g1Nord.Id,
            DamId = g1Snezhana.Id,
            BarnName = "Племрепродуктор №2 (Бычник)",
            SectionCode = "Денник Б-01",
            LineageCode = "Ситейшн Р 267150 (Аутбредный улучшатель)",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 965m,
            Notes = "Элита-рекорд. Индекс племенной ценности TPI +2680, рекомендуется для аутбредного кросса (F = 0%)."
        };

        var bullYenisey = new Animal
        {
            TagNumber = "RU-38-IRGAU-0012",
            RfidChip = "643098100038012",
            Name = "Енисей-Люкс",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Male,
            BirthDate = new DateTime(2022, 8, 19),
            SireId = g1Kedr.Id,
            DamId = g1Snezhana.Id,
            BarnName = "Племрепродуктор №2 (Бычник)",
            SectionCode = "Денник Б-02",
            LineageCode = "Вис Бэк Айдиал 1013415",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 920m,
            Notes = "Внук Султана-Идеала. Дает умеренный линейный кросс IV-III (F ≈ 1.56%) с потомками линии Олимпа."
        };

        var bullGrom = new Animal
        {
            TagNumber = "RU-38-IRGAU-0013",
            RfidChip = "643098100038013",
            Name = "Гром-Байкал",
            Species = AnimalSpecies.Cattle,
            Breed = "Черно-пестрая",
            Sex = AnimalSex.Male,
            BirthDate = new DateTime(2023, 2, 14),
            SireId = g1Baron.Id,
            DamId = g1Tayga.Id,
            BarnName = "Племрепродуктор №2 (Бычник)",
            SectionCode = "Денник Б-03",
            LineageCode = "Аннас Адема 30587 × Вис Бэк Айдиал",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 885m,
            Notes = "Несет комплекс генов Олимпа-Фаворита и Саяна-Монолита (повышенный риск близкого инбридинга F ≈ 4.69%)."
        };

        var bullBuran = new Animal
        {
            TagNumber = "RU-38-IRGAU-0014",
            RfidChip = "643098100038014",
            Name = "Буран-Гранд",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Male,
            BirthDate = new DateTime(2023, 3, 22),
            SireId = g1Cezar.Id,
            DamId = g1Snezhana.Id,
            BarnName = "Племрепродуктор №2 (Бычник)",
            SectionCode = "Денник Б-04",
            LineageCode = "Вис Бэк Айдиал 1013415",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 870m,
            Notes = "Сын Цезаря-Премиума! Полубрат коров Байкалка-7 и Зорька-4 (тесный инбридинг II-II, F = 12.50% — запрет спаривания с дочерьми Цезаря!)."
        };

        // Дойное и ремонтное поголовье коров/тёлок
        var cowBaykalka = new Animal
        {
            TagNumber = "RU-38-IRGAU-0107",
            RfidChip = "643098100038107",
            Name = "Байкалка-7",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Female,
            BirthDate = new DateTime(2023, 3, 12),
            SireId = g1Cezar.Id,
            DamId = g1Dvina.Id,
            BarnName = "Учебный корпус №1 (Молочный)",
            SectionCode = "Секция А / Стойло 07",
            LineageCode = "Вис Бэк Айдиал 1013415",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 615m,
            AverageDailyMilkKg = 32.4m,
            Notes = "Рекордистка учебной фермы ИрГАУ. Полная подтвержденная родословная на 6 поколений."
        };

        var cowZorka = new Animal
        {
            TagNumber = "RU-38-IRGAU-0104",
            RfidChip = "643098100038104",
            Name = "Зорька-4",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Female,
            BirthDate = new DateTime(2022, 11, 5),
            SireId = g1Cezar.Id,
            DamId = g2Chayka.Id,
            BarnName = "Учебный корпус №1 (Молочный)",
            SectionCode = "Секция А / Стойло 04",
            LineageCode = "Вис Бэк Айдиал 1013415",
            HealthStatus = HealthStatus.Healthy, // Сейчас получит лечение и перейдет в UnderTreatment (запишется в AnimalsHistory!)
            CurrentWeightKg = 592m,
            AverageDailyMilkKg = 27.8m,
            Notes = "Находится на антибиотикотерапии (Нитокс 200) — молоко строго сепарируется!"
        };

        var cowNochka = new Animal
        {
            TagNumber = "RU-38-IRGAU-0112",
            RfidChip = "643098100038112",
            Name = "Ночка-12",
            Species = AnimalSpecies.Cattle,
            Breed = "Черно-пестрая",
            Sex = AnimalSex.Female,
            BirthDate = new DateTime(2022, 7, 18),
            SireId = g1Baron.Id,
            DamId = g1Tayga.Id,
            BarnName = "Учебный корпус №1 (Молочный)",
            SectionCode = "Секция Б / Стойло 12",
            LineageCode = "Аннас Адема 30587",
            HealthStatus = HealthStatus.Healthy, // Перейдет в UnderTreatment после Амоксициллина (Temporal History!)
            CurrentWeightKg = 578m,
            AverageDailyMilkKg = 24.6m,
            Notes = "Послеродовой эндометрит, курс Амоксициллин 150 LA — активный карантинный замок!"
        };

        var cowAngara = new Animal
        {
            TagNumber = "RU-38-IRGAU-0109",
            RfidChip = "643098100038109",
            Name = "Ангара-9",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Female,
            BirthDate = new DateTime(2022, 9, 25),
            SireId = g1Kedr.Id,
            DamId = g1Dvina.Id,
            BarnName = "Учебный корпус №1 (Молочный)",
            SectionCode = "Секция А / Стойло 09",
            LineageCode = "Вис Бэк Айдиал 1013415",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 604m,
            AverageDailyMilkKg = 30.1m,
            Notes = "Клинически здорова, молоко высшего сорта."
        };

        var cowIrkutyanka = new Animal
        {
            TagNumber = "RU-38-IRGAU-0115",
            RfidChip = "643098100038115",
            Name = "Иркутянка-15",
            Species = AnimalSpecies.Cattle,
            Breed = "Черно-пестрая",
            Sex = AnimalSex.Female,
            BirthDate = new DateTime(2023, 1, 30),
            SireId = g2Vityaz.Id,
            DamId = g2Avrora.Id,
            BarnName = "Учебный корпус №1 (Молочный)",
            SectionCode = "Секция Б / Стойло 15",
            LineageCode = "Аннас Адема 30587",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 568m,
            AverageDailyMilkKg = 26.2m,
            Notes = "Адаптированный восточно-сибирский тип черно-пестрой породы."
        };

        var cowLastochka = new Animal
        {
            TagNumber = "RU-38-IRGAU-0118",
            RfidChip = "643098100038118",
            Name = "Ласточка-18",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Female,
            BirthDate = new DateTime(2023, 6, 10),
            SireId = g1Nord.Id,
            DamId = g1Dvina.Id,
            BarnName = "Учебный корпус №1 (Молочный)",
            SectionCode = "Секция Б / Стойло 18",
            LineageCode = "Ситейшн Р 267150",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 555m,
            AverageDailyMilkKg = 28.9m,
            Notes = "Первотелка, высокая скорость молокоотдачи."
        };

        var heiferMetelitsa = new Animal
        {
            TagNumber = "RU-38-IRGAU-0121",
            RfidChip = "643098100038121",
            Name = "Метелица-21",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Female,
            BirthDate = nowUtc.Date.AddMonths(-14),
            SireId = g1Nord.Id,
            DamId = g2Chayka.Id,
            BarnName = "Карантинный изолятор",
            SectionCode = "Бокс №2 (Приемный фильтр)",
            PreviousBarnBeforeQuarantine = "Племрепродуктор №2 (Молодняк) | Секция М-03",
            LineageCode = "Ситейшн Р 267150",
            HealthStatus = HealthStatus.Quarantine,
            QuarantineReason = "Профилактическое 30-дневное карантинирование племмолодняка и субфебрильная температура 39.6 °C",
            QuarantineEnteredAtUtc = nowUtc.AddDays(-9),
            CurrentWeightKg = 372m,
            Notes = "Ввезена из племзавода Красноярского края, под ежедневной термометрией ветфакультета ИрГАУ."
        };

        var calfOlkhon = new Animal
        {
            TagNumber = "RU-38-IRGAU-0130",
            RfidChip = "643098100038130",
            Name = "Ольхон-30",
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Female,
            BirthDate = nowUtc.Date.AddMonths(-5),
            SireId = bullAlmaz.Id,
            DamId = cowBaykalka.Id,
            BarnName = "Телятник №3 (Профилакторий)",
            SectionCode = "Клетка Т-05",
            LineageCode = "Ситейшн Р 267150",
            HealthStatus = HealthStatus.Healthy,
            CurrentWeightKg = 158m,
            Notes = "Ремонтная тёлочка от рекордистки Байкалки-7 и быка Алмаз-Сибирь (F = 0.00%)."
        };

        var activeAnimals = new[]
        {
            bullAlmaz, bullYenisey, bullGrom, bullBuran,
            cowBaykalka, cowZorka, cowNochka, cowAngara, cowIrkutyanka, cowLastochka,
            heiferMetelitsa, calfOlkhon
        };

        db.Animals.AddRange(activeAnimals);
        await db.SaveChangesAsync(ct);

        // =========================================================================
        // 4. ЖУРНАЛ ЛПМ И ДЕЙСТВУЮЩИЕ КАРАНТИННЫЕ ЗАМКИ (+ ГЕНЕРАЦИЯ ИСТОРИИ TEMPORAL TABLES!)
        // =========================================================================

        // 4.1. Историческое завершенное лечение у Ангары-9 (карантин уже истёк 12 дней назад)
        var pastTreatment = TreatmentRecord.Create(
            cowAngara,
            drugKetoprofen,
            18m,
            "внутримышечно",
            "Профилактика послеродового отёка вымени",
            nowUtc.AddDays(-18),
            "Д-р Соколов А.В., к.в.н.",
            "Главный ветврач",
            "Без периода браковки молока, курс завершен успешно.");
        pastTreatment.IsClearedNotificationSent = true;
        cowAngara.ChangeHealthStatus(HealthStatus.Healthy);

        // 4.2. АКТИВНЫЙ КАРАНТИННЫЙ ЗАМОК №1: Корова Зорька-4 — Нитокс 200 введен 22 часа назад (осталось ~146 часов из 168 ч!)
        var activeNitoxLock = TreatmentRecord.Create(
            cowZorka,
            drugNitox,
            58m,
            "глубоко внутримышечно",
            "Острый катаральный мастит правой задней четверти вымени",
            nowUtc.AddHours(-22),
            "Д-р Соколов А.В., к.в.н.",
            "Главный ветврач",
            "Первичная инъекция пролонгированного окситетрациклина.");

        // 4.3. АКТИВНЫЙ КАРАНТИННЫЙ ЗАМОК №2: Корова Ночка-12 — Амоксициллин 150 LA введен 34 часа назад (осталось ~62 часа из 96 ч!)
        var activeAmoxLock = TreatmentRecord.Create(
            cowNochka,
            drugAmox,
            55m,
            "внутримышечно",
            "Острый послеродовой катарально-гнойный эндометрит",
            nowUtc.AddHours(-34),
            "Д-р Соколов А.В., к.в.н.",
            "Главный ветврач",
            "Назначен строгий запрет слива молока в общий молокопровод на 96 часов.");

        db.TreatmentRecords.AddRange(pastTreatment, activeNitoxLock, activeAmoxLock);
        await db.SaveChangesAsync(ct);

        // Выполним уточнение клинического примечания, чтобы в SQL Server TreatmentRecordsHistory появилась архивная версия строки!
        activeNitoxLock.ClinicalNotes = "Контроль через 12 ч: отёк спал, температура 38.7 °C. Молоко сдаивается в санитарный бачок (утилизация).";
        await db.SaveChangesAsync(ct);

        // =========================================================================
        // 5. ДИНАМИКА УДОЕВ (12 ДНЕЙ) И ПРИВЕСОВ (6 МЕСЯЦЕВ) ДЛЯ ГРАФИКОВ КАРТОЧКИ
        // =========================================================================
        var milkingHerd = new[]
        {
            (Cow: cowBaykalka, BaseAm: 16.8m, BasePm: 15.6m, Fat: 4.05m),
            (Cow: cowAngara, BaseAm: 15.6m, BasePm: 14.5m, Fat: 3.95m),
            (Cow: cowLastochka, BaseAm: 15.0m, BasePm: 13.9m, Fat: 3.88m),
            (Cow: cowIrkutyanka, BaseAm: 13.7m, BasePm: 12.5m, Fat: 4.12m),
            (Cow: cowZorka, BaseAm: 14.2m, BasePm: 13.1m, Fat: 3.90m),
            (Cow: cowNochka, BaseAm: 12.8m, BasePm: 11.8m, Fat: 3.85m)
        };

        for (var dayOffset = -11; dayOffset <= 0; dayOffset++)
        {
            var date = nowUtc.Date.AddDays(dayOffset);
            foreach (var entry in milkingHerd)
            {
                var wave = (decimal)Math.Sin((dayOffset + 12) * 0.65) * 0.9m;
                var quarantinedToday = (entry.Cow.Id == cowZorka.Id && dayOffset >= -1)
                                       || (entry.Cow.Id == cowNochka.Id && dayOffset >= -1);

                db.MilkYieldRecords.Add(new MilkYieldRecord
                {
                    AnimalId = entry.Cow.Id,
                    MilkingDate = date,
                    MorningLiters = Math.Round(entry.BaseAm + wave, 1),
                    EveningLiters = Math.Round(entry.BasePm + (wave * 0.7m), 1),
                    FatPercent = entry.Fat,
                    ProteinPercent = 3.32m,
                    IsQuarantineDiscarded = quarantinedToday,
                    RecordedBy = "Петров М.И. (Бригадир МТФ №1)"
                });
            }
        }

        foreach (var animal in activeAnimals)
        {
            for (var m = 5; m >= 0; m--)
            {
                var factor = 1.0m - (m * 0.035m);
                var w = Math.Round(animal.CurrentWeightKg * factor, 1);
                db.WeightRecords.Add(new WeightRecord
                {
                    AnimalId = animal.Id,
                    MeasuredAtUtc = nowUtc.AddMonths(-m),
                    WeightKg = w,
                    DailyGainGrams = animal.Id == calfOlkhon.Id ? 840 : 620,
                    MeasuredBy = "Ильина Е.С. (Зоотехник-селекционер)"
                });
            }
        }

        // Осмотры по QR-коду
        db.VetInspections.AddRange(
            new VetInspection
            {
                AnimalId = cowBaykalka.Id,
                InspectedAtUtc = nowUtc.AddHours(-14),
                BodyTemperatureC = 38.5m,
                HeartRateBpm = 68,
                RuminationPer2Min = 4,
                ResultHealthStatus = HealthStatus.Healthy,
                Summary = "Клинически здорова, аппетит и жвачка активные, допуск к утренней дойке открыт.",
                InspectorName = "Д-р Соколов А.В., к.в.н.",
                ScannedViaQrPassport = true
            },
            new VetInspection
            {
                AnimalId = cowZorka.Id,
                InspectedAtUtc = nowUtc.AddHours(-22),
                BodyTemperatureC = 39.7m,
                HeartRateBpm = 86,
                RuminationPer2Min = 2,
                UdderCondition = "Уплотнение и гиперемия правой задней доли",
                ResultHealthStatus = HealthStatus.UnderTreatment,
                Summary = "Диагностирован острый катаральный мастит. Введен Нитокс 200, активирован карантинный замок на 168 ч.",
                InspectorName = "Д-р Соколов А.В., к.в.н.",
                ScannedViaQrPassport = true
            },
            new VetInspection
            {
                AnimalId = heiferMetelitsa.Id,
                InspectedAtUtc = nowUtc.AddHours(-8),
                BodyTemperatureC = 39.3m,
                HeartRateBpm = 80,
                RuminationPer2Min = 3,
                ResultHealthStatus = HealthStatus.Quarantine,
                Summary = "Продолжается 30-дневный карантин в Боксе №2 изолятора, взята парная сыворотка крови.",
                InspectorName = "Д-р Соколов А.В., к.в.н.",
                ScannedViaQrPassport = true
            });

        // =========================================================================
        // 6. ПАРТИИ МОЛОКА И ЖУРНАЛ СРАБАТЫВАНИЯ КАРАНТИННОГО ЗАМКА
        // =========================================================================
        var morningBatch = new ProductBatch
        {
            BatchNumber = $"MILK-{nowUtc.AddHours(-6):yyyyMMdd}-AM",
            BatchType = ProductBatchType.RawMilk,
            Status = BatchStatus.Approved,
            CreatedAtUtc = nowUtc.AddHours(-6),
            ApprovedAtUtc = nowUtc.AddHours(-5),
            FatPercent = 4.01m,
            ProteinPercent = 3.34m,
            SomaticCellsThousands = 132,
            Destination = "АО «Янтарь» (Иркутский молочный завод)",
            CreatedByOperator = "Петров М.И. (Оператор доильного зала)",
            ApprovedByVeterinarian = "Д-р Соколов А.В., к.в.н.",
            Notes = "Утренняя сборная партия охлажденного молока (+4 °C) от здоровой группы."
        };
        morningBatch.Items.Add(new ProductBatchItem { AnimalId = cowBaykalka.Id, QuantityLitersOrKg = 32.4m, RecordedAtUtc = nowUtc.AddHours(-6) });
        morningBatch.Items.Add(new ProductBatchItem { AnimalId = cowAngara.Id, QuantityLitersOrKg = 30.1m, RecordedAtUtc = nowUtc.AddHours(-6) });
        morningBatch.Items.Add(new ProductBatchItem { AnimalId = cowLastochka.Id, QuantityLitersOrKg = 28.9m, RecordedAtUtc = nowUtc.AddHours(-6) });
        morningBatch.Items.Add(new ProductBatchItem { AnimalId = cowIrkutyanka.Id, QuantityLitersOrKg = 26.2m, RecordedAtUtc = nowUtc.AddHours(-6) });
        morningBatch.TotalVolumeLitersOrKg = morningBatch.Items.Sum(x => x.QuantityLitersOrKg);

        db.ProductBatches.Add(morningBatch);

        db.QuarantineBlockLogs.Add(new QuarantineBlockLog
        {
            AttemptedAtUtc = nowUtc.AddHours(-4),
            AttemptedBatchNumber = $"MILK-{nowUtc:yyyyMMdd}-TEST01",
            BatchType = ProductBatchType.RawMilk,
            AnimalId = cowZorka.Id,
            AnimalTag = cowZorka.TagNumber,
            AnimalName = cowZorka.Name,
            ActiveDrugName = drugNitox.Name,
            ActiveDrugSeries = drugNitox.BatchSeries,
            RemainingHours = 150.0,
            AttemptedByUser = "Петров М.И. (Оператор доильного зала)",
            BlockMessage = $"КАРАНТИННЫЙ ЗАМОК! Запрещено включение животного {cowZorka.Name} [{cowZorka.TagNumber}] в товарное молоко. Активный препарат: «{drugNitox.Name}» (серия {drugNitox.BatchSeries}). Остаток карантина: 150 ч 00 мин."
        });

        // =========================================================================
        // 7. ПРОТИВОЭПИЗООТИЧЕСКИЙ КАЛЕНДАРЬ И ОБЯЗАТЕЛЬНЫЕ ПРАВИЛА МИНСЕЛЬХОЗА РФ
        // =========================================================================
        var ruleAnthrax = new EpizooticRule
        {
            DiseaseType = EpizooticDiseaseType.Anthrax,
            EventType = EpizooticEventType.Vaccination,
            Title = "Профилактическая вакцинация КРС против сибирской язвы",
            MinAgeDays = 90,
            IntervalDays = 365,
            DefaultVetDrugId = vacAnthrax.Id,
            StandardDosePerAnimal = 1.0m,
            RegulatoryBasis = "Приказ Минсельхоза РФ № 537 (Ветеринарные правила по сибирской язве)",
            IsMandatory = true
        };

        var ruleTuberculosis = new EpizooticRule
        {
            DiseaseType = EpizooticDiseaseType.Tuberculosis,
            EventType = EpizooticEventType.DiagnosticTest,
            Title = "Плановая аллергическая туберкулинизация (внутрикожная проба ППД)",
            MinAgeDays = 60,
            IntervalDays = 180,
            DefaultVetDrugId = diagTuberculin.Id,
            StandardDosePerAnimal = 1.0m,
            RegulatoryBasis = "Приказ Минсельхоза РФ № 534 (Ветеринарные правила по туберкулёзу)",
            IsMandatory = true
        };

        var ruleBrucellosis = new EpizooticRule
        {
            DiseaseType = EpizooticDiseaseType.Brucellosis,
            EventType = EpizooticEventType.DiagnosticTest,
            Title = "Серологическое исследование сыворотки крови на бруцеллёз (РА / РСК)",
            MinAgeDays = 120,
            IntervalDays = 180,
            DefaultVetDrugId = diagBrucellosis.Id,
            StandardDosePerAnimal = 1.0m,
            RegulatoryBasis = "Приказ Минсельхоза РФ № 533 (Ветеринарные правила по бруцеллёзу)",
            IsMandatory = true
        };

        var ruleLeukosis = new EpizooticRule
        {
            DiseaseType = EpizooticDiseaseType.Leukosis,
            EventType = EpizooticEventType.DiagnosticTest,
            Title = "Серологическое исследование на вирус лейкоза КРС (РИД)",
            MinAgeDays = 180,
            IntervalDays = 180,
            DefaultVetDrugId = diagLeukosis.Id,
            StandardDosePerAnimal = 1.0m,
            RegulatoryBasis = "Приказ Минсельхоза РФ № 156 (Ветеринарные правила по лейкозу КРС)",
            IsMandatory = true
        };

        db.EpizooticRules.AddRange(ruleAnthrax, ruleTuberculosis, ruleBrucellosis, ruleLeukosis);
        await db.SaveChangesAsync(ct);

        // Весенний акт вакцинации сибирской язвы для части стада (чтобы часть животных была зеленой Compliant, а молодняк/новые — в плане!)
        var pastAnthraxAct = new EpizooticAct
        {
            ActNumber = $"АКТ-ЭПИЗ-{nowUtc.AddMonths(-5):yyyyMMdd}-01",
            EpizooticRuleId = ruleAnthrax.Id,
            VetDrugId = vacAnthrax.Id,
            DrugNameSnapshot = vacAnthrax.Name,
            DrugSeriesSnapshot = vacAnthrax.BatchSeries,
            ExecutedAtUtc = nowUtc.AddMonths(-5),
            TargetBarn = "Учебный корпус №1 и Бычник №2",
            DosePerAnimal = 1.0m,
            TotalDosesUsed = 8.0m,
            ExecutedByVeterinarian = "Д-р Соколов А.В., к.в.н.",
            WitnessZootechnician = "Ильина Е.С. (Зоотехник-селекционер)",
            Notes = "Плановая весенняя иммунизация взрослого поголовья ИрГАУ, осложнений не выявлено."
        };

        foreach (var vaccinated in new[] { bullAlmaz, bullYenisey, bullGrom, bullBuran, cowBaykalka, cowZorka, cowNochka, cowAngara })
        {
            pastAnthraxAct.Items.Add(new EpizooticActItem
            {
                AnimalId = vaccinated.Id,
                ResultText = "Привито подкожно 1.0 мл, реакция в норме",
                NextDueAtUtc = nowUtc.AddMonths(7)
            });
        }

        // Осенний акт бруцеллёза и лейкоза для быков (чтобы дойный корпус требовал 2-кликовой групповой обработки на демо!)
        var pastBrucellosisAct = new EpizooticAct
        {
            ActNumber = $"АКТ-ЭПИЗ-{nowUtc.AddMonths(-2):yyyyMMdd}-02",
            EpizooticRuleId = ruleBrucellosis.Id,
            VetDrugId = diagBrucellosis.Id,
            DrugNameSnapshot = diagBrucellosis.Name,
            DrugSeriesSnapshot = diagBrucellosis.BatchSeries,
            ExecutedAtUtc = nowUtc.AddMonths(-2),
            TargetBarn = "Племрепродуктор №2 (Бычник)",
            DosePerAnimal = 1.0m,
            TotalDosesUsed = 4.0m,
            ExecutedByVeterinarian = "Д-р Соколов А.В., к.в.н.",
            WitnessZootechnician = "Ильина Е.С."
        };

        foreach (var bull in new[] { bullAlmaz, bullYenisey, bullGrom, bullBuran })
        {
            pastBrucellosisAct.Items.Add(new EpizooticActItem
            {
                AnimalId = bull.Id,
                ResultText = "РА и РСК — отрицательно",
                NextDueAtUtc = nowUtc.AddMonths(4)
            });
        }

        db.EpizooticActs.AddRange(pastAnthraxAct, pastBrucellosisAct);

        // Демонстрационный утвержденный племенной план подбора пары
        db.BreedingPlans.Add(new BreedingPlan
        {
            DamId = cowBaykalka.Id,
            SireId = bullAlmaz.Id,
            InbreedingCoefficientPercent = 0.0000m,
            ShaporuzhNotation = "Аутбридинг (общих предков нет)",
            RiskLevel = InbreedingRiskLevel.Safe,
            Status = BreedingPlanStatus.Recommended,
            CommonAncestorsSummary = "Общих предков до VI колена родословной не выявлено",
            PlannedAtUtc = nowUtc.AddDays(-3),
            CreatedBySelector = "Ильина Е.С. (Зоотехник-селекционер)",
            Notes = "Оптимальное закрепление на следующую охоту для получения высокопродуктивного ремонтного молодняка."
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Наполнение БД IrGau_VetGuardDb успешно завершено: 41 животное в родословной книге (12 активных + 29 предков до VI колена).");
    }
}
