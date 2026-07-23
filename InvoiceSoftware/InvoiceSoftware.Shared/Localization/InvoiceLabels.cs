using System.Globalization;
using InvoiceSoftware.Shared.Enums;

namespace InvoiceSoftware.Shared.Localization;

/// <summary>
/// The fixed furniture of an invoice document — "Subtotal", "Bill To", "Amount
/// Due" — in each language Akinai supports.
///
/// These are held in code rather than sent through Azure Translator for three
/// reasons. They never change, so paying to translate them on every invoice
/// view would be pure waste against a shared character allowance. They are
/// terms of art on a financial document, where "Tax" must come back as the
/// word a local accountant expects rather than whatever a general-purpose
/// engine picks. And they must render even when translation is unconfigured
/// or the API is down — a client should never receive an invoice with half
/// its headings in the wrong language.
///
/// Machine translation is reserved for what genuinely varies: the freeform
/// text the user wrote themselves.
/// </summary>
public record InvoiceLabelSet(
    string CultureName,
    string Invoice,
    string StatusPaid,
    string StatusOverdue,
    /// <summary>{0} is the number of days.</summary>
    string DaysPastDue,
    string StatusPaymentDue,
    /// <summary>{0} is the due date, already formatted.</summary>
    string DueByDate,
    string StatusDraft,
    string DraftNotSent,
    string StatusVoid,
    string VoidSubtitle,
    string Issued,
    string Due,
    string BillTo,
    string AmountDue,
    string PaidInFull,
    string Services,
    string Expenses,
    string Products,
    string Description,
    string Hours,
    string Rate,
    string Amount,
    string Qty,
    string Price,
    string Category,
    string Date,
    string Subtotal,
    string Tax,
    string Total,
    string Notes,
    string PaymentOptions,
    string BankTransfer,
    string ScanWithBankingApp,
    string PayWithPayPal,
    string PayWithWise,
    string PayWithRevolut,
    /// <summary>Footer line. {0} is the due date, already formatted for the culture.</summary>
    string ThanksAndDueBy)
{
    /// <summary>
    /// The culture to format this invoice's dates and numbers with.
    ///
    /// Falls back to invariant rather than throwing. On WebAssembly the ICU
    /// data for an arbitrary culture is not guaranteed to have been loaded,
    /// and an invoice that renders with slightly unfamiliar date order is a
    /// far smaller problem than one that fails to render at all.
    /// </summary>
    public CultureInfo Culture
    {
        get
        {
            try { return CultureInfo.GetCultureInfo(CultureName); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }
    }
}

public static class InvoiceLabels
{
    private static readonly InvoiceLabelSet English = new(
        CultureName: "en-GB",
        Invoice: "INVOICE",
        StatusPaid: "Paid",
        StatusOverdue: "Payment Overdue",
        DaysPastDue: "{0} days past due",
        StatusPaymentDue: "Payment Due",
        DueByDate: "by {0}",
        StatusDraft: "Draft",
        DraftNotSent: "Not yet sent",
        StatusVoid: "Void",
        VoidSubtitle: "This invoice has been voided",
        Issued: "Issued",
        Due: "Due",
        BillTo: "Bill To",
        AmountDue: "Amount Due",
        PaidInFull: "Paid in full",
        Services: "Services",
        Expenses: "Expenses",
        Products: "Products",
        Description: "Description",
        Hours: "Hours",
        Rate: "Rate",
        Amount: "Amount",
        Qty: "Qty",
        Price: "Price",
        Category: "Category",
        Date: "Date",
        Subtotal: "Subtotal",
        Tax: "Tax",
        Total: "Total",
        Notes: "Notes",
        PaymentOptions: "Payment Options",
        BankTransfer: "Bank Transfer",
        ScanWithBankingApp: "Scan with your banking app",
        PayWithPayPal: "Pay with PayPal",
        PayWithWise: "Pay with Wise",
        PayWithRevolut: "Pay with Revolut",
        ThanksAndDueBy: "Thank you for your business. Payment is due by {0}.");

    private static readonly IReadOnlyDictionary<SupportedLanguage, InvoiceLabelSet> Sets =
        new Dictionary<SupportedLanguage, InvoiceLabelSet>
        {
            [SupportedLanguage.English] = English,

            [SupportedLanguage.Spanish] = new(
                CultureName: "es-ES",
                Invoice: "FACTURA",
                StatusPaid: "Pagada",
                StatusOverdue: "Pago vencido",
                DaysPastDue: "{0} días de retraso",
                StatusPaymentDue: "Pago pendiente",
                DueByDate: "antes del {0}",
                StatusDraft: "Borrador",
                DraftNotSent: "Aún no enviada",
                StatusVoid: "Anulada",
                VoidSubtitle: "Esta factura ha sido anulada",
                Issued: "Emitida",
                Due: "Vence",
                BillTo: "Facturar a",
                AmountDue: "Importe a pagar",
                PaidInFull: "Pagado en su totalidad",
                Services: "Servicios",
                Expenses: "Gastos",
                Products: "Productos",
                Description: "Descripción",
                Hours: "Horas",
                Rate: "Tarifa",
                Amount: "Importe",
                Qty: "Cant.",
                Price: "Precio",
                Category: "Categoría",
                Date: "Fecha",
                Subtotal: "Subtotal",
                Tax: "Impuesto",
                Total: "Total",
                Notes: "Notas",
                PaymentOptions: "Formas de pago",
                BankTransfer: "Transferencia bancaria",
                ScanWithBankingApp: "Escanee con su aplicación bancaria",
                PayWithPayPal: "Pagar con PayPal",
                PayWithWise: "Pagar con Wise",
                PayWithRevolut: "Pagar con Revolut",
                ThanksAndDueBy: "Gracias por su confianza. El pago vence el {0}."),

            [SupportedLanguage.French] = new(
                CultureName: "fr-FR",
                Invoice: "FACTURE",
                StatusPaid: "Payée",
                StatusOverdue: "Paiement en retard",
                DaysPastDue: "{0} jours de retard",
                StatusPaymentDue: "Paiement à échéance",
                DueByDate: "avant le {0}",
                StatusDraft: "Brouillon",
                DraftNotSent: "Pas encore envoyée",
                StatusVoid: "Annulée",
                VoidSubtitle: "Cette facture a été annulée",
                Issued: "Émise le",
                Due: "Échéance",
                BillTo: "Facturer à",
                AmountDue: "Montant dû",
                PaidInFull: "Payée intégralement",
                Services: "Prestations",
                Expenses: "Frais",
                Products: "Produits",
                Description: "Description",
                Hours: "Heures",
                Rate: "Tarif",
                Amount: "Montant",
                Qty: "Qté",
                Price: "Prix",
                Category: "Catégorie",
                Date: "Date",
                Subtotal: "Sous-total",
                Tax: "TVA",
                Total: "Total",
                Notes: "Remarques",
                PaymentOptions: "Moyens de paiement",
                BankTransfer: "Virement bancaire",
                ScanWithBankingApp: "Scannez avec votre application bancaire",
                PayWithPayPal: "Payer avec PayPal",
                PayWithWise: "Payer avec Wise",
                PayWithRevolut: "Payer avec Revolut",
                ThanksAndDueBy: "Merci de votre confiance. Le paiement est dû le {0}."),

            [SupportedLanguage.German] = new(
                CultureName: "de-DE",
                Invoice: "RECHNUNG",
                StatusPaid: "Bezahlt",
                StatusOverdue: "Zahlung überfällig",
                DaysPastDue: "{0} Tage überfällig",
                StatusPaymentDue: "Zahlung fällig",
                DueByDate: "bis {0}",
                StatusDraft: "Entwurf",
                DraftNotSent: "Noch nicht versendet",
                StatusVoid: "Storniert",
                VoidSubtitle: "Diese Rechnung wurde storniert",
                Issued: "Ausgestellt",
                Due: "Fällig",
                BillTo: "Rechnungsempfänger",
                AmountDue: "Fälliger Betrag",
                PaidInFull: "Vollständig bezahlt",
                Services: "Leistungen",
                Expenses: "Auslagen",
                Products: "Produkte",
                Description: "Beschreibung",
                Hours: "Stunden",
                Rate: "Satz",
                Amount: "Betrag",
                Qty: "Menge",
                Price: "Preis",
                Category: "Kategorie",
                Date: "Datum",
                Subtotal: "Zwischensumme",
                Tax: "USt.",
                Total: "Gesamt",
                Notes: "Anmerkungen",
                PaymentOptions: "Zahlungsmöglichkeiten",
                BankTransfer: "Banküberweisung",
                ScanWithBankingApp: "Mit Ihrer Banking-App scannen",
                PayWithPayPal: "Mit PayPal bezahlen",
                PayWithWise: "Mit Wise bezahlen",
                PayWithRevolut: "Mit Revolut bezahlen",
                ThanksAndDueBy: "Vielen Dank für Ihren Auftrag. Zahlbar bis {0}."),

            [SupportedLanguage.Chinese] = new(
                CultureName: "zh-CN",
                Invoice: "发票",
                StatusPaid: "已支付",
                StatusOverdue: "逾期未付",
                DaysPastDue: "逾期 {0} 天",
                StatusPaymentDue: "待付款",
                DueByDate: "截止 {0}",
                StatusDraft: "草稿",
                DraftNotSent: "尚未发送",
                StatusVoid: "已作废",
                VoidSubtitle: "此发票已作废",
                Issued: "开票日期",
                Due: "到期日期",
                BillTo: "收票方",
                AmountDue: "应付金额",
                PaidInFull: "已全额支付",
                Services: "服务",
                Expenses: "费用",
                Products: "产品",
                Description: "说明",
                Hours: "小时",
                Rate: "费率",
                Amount: "金额",
                Qty: "数量",
                Price: "单价",
                Category: "类别",
                Date: "日期",
                Subtotal: "小计",
                Tax: "税费",
                Total: "合计",
                Notes: "备注",
                PaymentOptions: "支付方式",
                BankTransfer: "银行转账",
                ScanWithBankingApp: "请用您的银行应用扫描",
                PayWithPayPal: "使用 PayPal 支付",
                PayWithWise: "使用 Wise 支付",
                PayWithRevolut: "使用 Revolut 支付",
                ThanksAndDueBy: "感谢您的惠顾。付款截止日期为 {0}。"),

            [SupportedLanguage.Vietnamese] = new(
                CultureName: "vi-VN",
                Invoice: "HÓA ĐƠN",
                StatusPaid: "Đã thanh toán",
                StatusOverdue: "Quá hạn thanh toán",
                DaysPastDue: "Quá hạn {0} ngày",
                StatusPaymentDue: "Chờ thanh toán",
                DueByDate: "trước ngày {0}",
                StatusDraft: "Bản nháp",
                DraftNotSent: "Chưa gửi",
                StatusVoid: "Đã hủy",
                VoidSubtitle: "Hóa đơn này đã bị hủy",
                Issued: "Ngày phát hành",
                Due: "Hạn thanh toán",
                BillTo: "Bên nhận hóa đơn",
                AmountDue: "Số tiền phải trả",
                PaidInFull: "Đã thanh toán đầy đủ",
                Services: "Dịch vụ",
                Expenses: "Chi phí",
                Products: "Sản phẩm",
                Description: "Mô tả",
                Hours: "Giờ",
                Rate: "Đơn giá",
                Amount: "Thành tiền",
                Qty: "SL",
                Price: "Giá",
                Category: "Danh mục",
                Date: "Ngày",
                Subtotal: "Tạm tính",
                Tax: "Thuế",
                Total: "Tổng cộng",
                Notes: "Ghi chú",
                PaymentOptions: "Phương thức thanh toán",
                BankTransfer: "Chuyển khoản ngân hàng",
                ScanWithBankingApp: "Quét bằng ứng dụng ngân hàng của bạn",
                PayWithPayPal: "Thanh toán bằng PayPal",
                PayWithWise: "Thanh toán bằng Wise",
                PayWithRevolut: "Thanh toán bằng Revolut",
                ThanksAndDueBy: "Cảm ơn quý khách. Hạn thanh toán là {0}."),

            [SupportedLanguage.Russian] = new(
                CultureName: "ru-RU",
                Invoice: "СЧЁТ",
                StatusPaid: "Оплачен",
                StatusOverdue: "Просрочен",
                DaysPastDue: "Просрочено на {0} дн.",
                StatusPaymentDue: "Ожидает оплаты",
                DueByDate: "до {0}",
                StatusDraft: "Черновик",
                DraftNotSent: "Ещё не отправлен",
                StatusVoid: "Аннулирован",
                VoidSubtitle: "Этот счёт аннулирован",
                Issued: "Выставлен",
                Due: "Срок оплаты",
                BillTo: "Плательщик",
                AmountDue: "Сумма к оплате",
                PaidInFull: "Оплачено полностью",
                Services: "Услуги",
                Expenses: "Расходы",
                Products: "Товары",
                Description: "Описание",
                Hours: "Часы",
                Rate: "Ставка",
                Amount: "Сумма",
                Qty: "Кол-во",
                Price: "Цена",
                Category: "Категория",
                Date: "Дата",
                Subtotal: "Промежуточный итог",
                Tax: "НДС",
                Total: "Итого",
                Notes: "Примечания",
                PaymentOptions: "Способы оплаты",
                BankTransfer: "Банковский перевод",
                ScanWithBankingApp: "Отсканируйте в приложении банка",
                PayWithPayPal: "Оплатить через PayPal",
                PayWithWise: "Оплатить через Wise",
                PayWithRevolut: "Оплатить через Revolut",
                ThanksAndDueBy: "Благодарим за сотрудничество. Оплата до {0}."),

            [SupportedLanguage.Korean] = new(
                CultureName: "ko-KR",
                Invoice: "청구서",
                StatusPaid: "결제 완료",
                StatusOverdue: "결제 기한 초과",
                DaysPastDue: "{0}일 연체",
                StatusPaymentDue: "결제 예정",
                DueByDate: "{0}까지",
                StatusDraft: "임시 저장",
                DraftNotSent: "아직 발송되지 않음",
                StatusVoid: "취소됨",
                VoidSubtitle: "이 청구서는 취소되었습니다",
                Issued: "발행일",
                Due: "지급기일",
                BillTo: "청구 대상",
                AmountDue: "청구 금액",
                PaidInFull: "전액 결제 완료",
                Services: "용역",
                Expenses: "경비",
                Products: "제품",
                Description: "내역",
                Hours: "시간",
                Rate: "단가",
                Amount: "금액",
                Qty: "수량",
                Price: "단가",
                Category: "분류",
                Date: "일자",
                Subtotal: "소계",
                Tax: "부가세",
                Total: "합계",
                Notes: "비고",
                PaymentOptions: "결제 방법",
                BankTransfer: "계좌 이체",
                ScanWithBankingApp: "은행 앱으로 스캔하세요",
                PayWithPayPal: "PayPal로 결제",
                PayWithWise: "Wise로 결제",
                PayWithRevolut: "Revolut로 결제",
                ThanksAndDueBy: "거래해 주셔서 감사합니다. 결제 기한은 {0}입니다."),
        };

    /// <summary>
    /// Expense categories. A closed enum, so these are labels rather than
    /// machine translation — same reasoning as the rest of this file, plus the
    /// stored names are PascalCase ("OfficeSupplies") and were never fit to
    /// show a client in any language.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<SupportedLanguage, string>> Categories =
        new Dictionary<string, IReadOnlyDictionary<SupportedLanguage, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Travel"] = Map("Travel", "Viajes", "Déplacements", "Reisekosten", "差旅", "Đi lại", "Поездки", "여행"),
            ["OfficeSupplies"] = Map("Office Supplies", "Material de oficina", "Fournitures de bureau", "Bürobedarf", "办公用品", "Văn phòng phẩm", "Канцтовары", "사무용품"),
            ["Software"] = Map("Software", "Software", "Logiciels", "Software", "软件", "Phần mềm", "ПО", "소프트웨어"),
            ["Meals"] = Map("Meals", "Comidas", "Repas", "Verpflegung", "餐饮", "Ăn uống", "Питание", "식비"),
            ["Utilities"] = Map("Utilities", "Suministros", "Charges", "Nebenkosten", "水电费", "Tiện ích", "Коммунальные услуги", "공과금"),
            ["Marketing"] = Map("Marketing", "Marketing", "Marketing", "Marketing", "市场营销", "Tiếp thị", "Маркетинг", "마케팅"),
            ["ProfessionalServices"] = Map("Professional Services", "Servicios profesionales", "Services professionnels", "Dienstleistungen", "专业服务", "Dịch vụ chuyên môn", "Профессиональные услуги", "전문 용역"),
            ["Equipment"] = Map("Equipment", "Equipamiento", "Équipement", "Ausstattung", "设备", "Thiết bị", "Оборудование", "장비"),
            ["Other"] = Map("Other", "Otros", "Autres", "Sonstiges", "其他", "Khác", "Прочее", "기타"),
        };

    private static IReadOnlyDictionary<SupportedLanguage, string> Map(
        string en, string es, string fr, string de, string zh, string vi, string ru, string ko)
        => new Dictionary<SupportedLanguage, string>
        {
            [SupportedLanguage.English] = en,
            [SupportedLanguage.Spanish] = es,
            [SupportedLanguage.French] = fr,
            [SupportedLanguage.German] = de,
            [SupportedLanguage.Chinese] = zh,
            [SupportedLanguage.Vietnamese] = vi,
            [SupportedLanguage.Russian] = ru,
            [SupportedLanguage.Korean] = ko,
        };

    /// <summary>
    /// A displayable expense category name. Unknown categories fall back to the
    /// raw stored value rather than throwing — a new enum member must not be
    /// able to break an invoice before its translations are filled in.
    /// </summary>
    public static string ExpenseCategory(string? languageName, string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return "";
        if (!Categories.TryGetValue(categoryName, out var byLanguage)) return categoryName;

        var language = Enum.TryParse<SupportedLanguage>(languageName, ignoreCase: true, out var parsed)
            ? parsed
            : SupportedLanguage.English;

        return byLanguage.TryGetValue(language, out var label)
            ? label
            : byLanguage[SupportedLanguage.English];
    }

    /// <summary>Labels for a language, falling back to English for anything unmapped.</summary>
    public static InvoiceLabelSet For(SupportedLanguage language)
        => Sets.TryGetValue(language, out var set) ? set : English;

    /// <summary>
    /// Labels for a language name as it crosses the wire (the enum is sent as a
    /// string). Unrecognised names fall back to English rather than throwing —
    /// a stale value in the database must not break an invoice.
    /// </summary>
    public static InvoiceLabelSet For(string? languageName)
        => Enum.TryParse<SupportedLanguage>(languageName, ignoreCase: true, out var parsed)
            ? For(parsed)
            : English;
}
