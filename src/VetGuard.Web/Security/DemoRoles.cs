namespace VetGuard.Web.Security;

public sealed record DemoUserProfile(
    string Key,
    string Role,
    string RoleTitle,
    string FullName,
    string Department,
    string BadgeIcon,
    string Description);

public static class DemoRoles
{
    public const string ChiefVet = "ChiefVet";
    public const string Zootechnician = "Zootechnician";
    public const string MilkingOperator = "MilkingOperator";

    public static readonly IReadOnlyList<DemoUserProfile> Profiles = new[]
    {
        new DemoUserProfile(
            Key: ChiefVet,
            Role: ChiefVet,
            RoleTitle: "Главный ветврач",
            FullName: "Д-р Соколов А.В., к.в.н.",
            Department: "Ветслужба Учхоза ИрГАУ",
            BadgeIcon: "⚕️",
            Description: "Назначение антибиотиков и ветпрепаратов, контроль «Карантинного замка», допуск товарных партий (ВСД), групповые акты вакцинаций, карантинный изолятор и аудит Temporal Tables."),
        new DemoUserProfile(
            Key: Zootechnician,
            Role: Zootechnician,
            RoleTitle: "Зоотехник-селекционер",
            FullName: "Ильина Е.С.",
            Department: "Племенной отдел ИрГАУ",
            BadgeIcon: "🧬",
            Description: "Племенной радар и родословные до 6 колен (T-SQL Recursive CTE), анти-инбридинг подбор пар по С. Райту и Шапоружу, бонитировка привесов и стойловые QR-паспорта."),
        new DemoUserProfile(
            Key: MilkingOperator,
            Role: MilkingOperator,
            RoleTitle: "Оператор доильного зала / Бригадир",
            FullName: "Петров М.И.",
            Department: "Молочно-товарная ферма №1",
            BadgeIcon: "🥛",
            Description: "Учет суточных удоев, формирование сборных танков товарного молока и проверка жесткой блокировки при попытке слива карантинного молока.")
    };

    public static DemoUserProfile GetByKey(string? key)
        => Profiles.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? Profiles[0];
}
