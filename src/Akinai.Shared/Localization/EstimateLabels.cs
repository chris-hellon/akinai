using System.Globalization;
using Akinai.Shared.Enums;

namespace Akinai.Shared.Localization;

/// <summary>
/// The fixed furniture of an estimate document, in each language Akinai
/// supports — the estimate counterpart to <see cref="InvoiceLabels"/>, and held
/// in code for the same reasons: free, instant, and correct in the sense a
/// local accountant would recognise.
///
/// Kept as its own set rather than folded into the invoice one because the two
/// documents genuinely differ. An estimate is not yet owed, so it has no "Amount
/// Due" and no payment rails; it expires rather than falling overdue; and it is
/// accepted or declined rather than paid. Several languages draw that
/// distinction with different words — German "Angebot" against "Rechnung",
/// Vietnamese "Báo giá" against "Hóa đơn" — so sharing a set would force one of
/// the two documents to use the other's vocabulary.
/// </summary>
public record EstimateLabelSet(
    string CultureName,
    string Estimate,
    string StatusAccepted,
    string StatusRejected,
    string StatusConverted,
    string StatusExpired,
    /// <summary>{0} is the number of days.</summary>
    string DaysAgo,
    string StatusAwaiting,
    /// <summary>{0} is the expiry date, already formatted.</summary>
    string ValidUntilDate,
    string StatusDraft,
    string DraftNotSent,
    string Date,
    string ValidUntil,
    string PreparedFor,
    string EstimatedTotal,
    string Items,
    string Description,
    string Qty,
    string Rate,
    string Amount,
    string Subtotal,
    string Tax,
    string Total,
    string Notes,
    string Terms,
    string TaxVat,
    string Registration,
    /// <summary>Footer line. {0} is the expiry date, already formatted.</summary>
    string ValidUntilFooter,
    /// <summary>Footer line used when the estimate has no expiry date.</summary>
    string ThanksForConsidering,
    /// <summary>Branding line, shown only on plans without RemoveBranding.</summary>
    string CreatedWith)
{
    /// <inheritdoc cref="InvoiceLabelSet.Culture"/>
    public CultureInfo Culture
    {
        get
        {
            try { return CultureInfo.GetCultureInfo(CultureName); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }
    }
}

public static class EstimateLabels
{
    private static readonly EstimateLabelSet English = new(
        CultureName: "en-GB",
        Estimate: "ESTIMATE",
        StatusAccepted: "Accepted",
        StatusRejected: "Declined",
        StatusConverted: "Converted to Invoice",
        StatusExpired: "Expired",
        DaysAgo: "{0} days ago",
        StatusAwaiting: "Awaiting Response",
        ValidUntilDate: "Valid until {0}",
        StatusDraft: "Draft",
        DraftNotSent: "Not yet sent",
        Date: "Date",
        ValidUntil: "Valid Until",
        PreparedFor: "Prepared For",
        EstimatedTotal: "Estimated Total",
        Items: "Items",
        Description: "Description",
        Qty: "Qty",
        Rate: "Rate",
        Amount: "Amount",
        Subtotal: "Subtotal",
        Tax: "Tax",
        Total: "Total",
        Notes: "Notes",
        Terms: "Terms & Conditions",
        TaxVat: "Tax/VAT",
        Registration: "Reg",
        ValidUntilFooter: "This estimate is valid until {0}.",
        ThanksForConsidering: "Thank you for considering our services.",
        CreatedWith: "Created with Akinai");

    private static readonly IReadOnlyDictionary<SupportedLanguage, EstimateLabelSet> Sets =
        new Dictionary<SupportedLanguage, EstimateLabelSet>
        {
            [SupportedLanguage.English] = English,

            [SupportedLanguage.Spanish] = new(
                CultureName: "es-ES",
                Estimate: "PRESUPUESTO",
                StatusAccepted: "Aceptado",
                StatusRejected: "Rechazado",
                StatusConverted: "Convertido en factura",
                StatusExpired: "Caducado",
                DaysAgo: "hace {0} días",
                StatusAwaiting: "Pendiente de respuesta",
                ValidUntilDate: "Válido hasta el {0}",
                StatusDraft: "Borrador",
                DraftNotSent: "Aún no enviado",
                Date: "Fecha",
                ValidUntil: "Válido hasta",
                PreparedFor: "Preparado para",
                EstimatedTotal: "Total estimado",
                Items: "Conceptos",
                Description: "Descripción",
                Qty: "Cant.",
                Rate: "Precio",
                Amount: "Importe",
                Subtotal: "Subtotal",
                Tax: "Impuesto",
                Total: "Total",
                Notes: "Notas",
                Terms: "Términos y condiciones",
                TaxVat: "NIF/IVA",
                Registration: "Reg.",
                ValidUntilFooter: "Este presupuesto es válido hasta el {0}.",
                ThanksForConsidering: "Gracias por considerar nuestros servicios.",
                CreatedWith: "Creado con Akinai"),

            [SupportedLanguage.French] = new(
                CultureName: "fr-FR",
                Estimate: "DEVIS",
                StatusAccepted: "Accepté",
                StatusRejected: "Refusé",
                StatusConverted: "Converti en facture",
                StatusExpired: "Expiré",
                DaysAgo: "il y a {0} jours",
                StatusAwaiting: "En attente de réponse",
                ValidUntilDate: "Valable jusqu'au {0}",
                StatusDraft: "Brouillon",
                DraftNotSent: "Pas encore envoyé",
                Date: "Date",
                ValidUntil: "Valable jusqu'au",
                PreparedFor: "Préparé pour",
                EstimatedTotal: "Total estimé",
                Items: "Prestations",
                Description: "Description",
                Qty: "Qté",
                Rate: "Prix unitaire",
                Amount: "Montant",
                Subtotal: "Sous-total",
                Tax: "TVA",
                Total: "Total",
                Notes: "Notes",
                Terms: "Conditions générales",
                TaxVat: "N° TVA",
                Registration: "SIRET",
                ValidUntilFooter: "Ce devis est valable jusqu'au {0}.",
                ThanksForConsidering: "Merci de l'intérêt porté à nos services.",
                CreatedWith: "Créé avec Akinai"),

            [SupportedLanguage.German] = new(
                CultureName: "de-DE",
                Estimate: "ANGEBOT",
                StatusAccepted: "Angenommen",
                StatusRejected: "Abgelehnt",
                StatusConverted: "In Rechnung umgewandelt",
                StatusExpired: "Abgelaufen",
                DaysAgo: "vor {0} Tagen",
                StatusAwaiting: "Warten auf Rückmeldung",
                ValidUntilDate: "Gültig bis {0}",
                StatusDraft: "Entwurf",
                DraftNotSent: "Noch nicht gesendet",
                Date: "Datum",
                ValidUntil: "Gültig bis",
                PreparedFor: "Erstellt für",
                EstimatedTotal: "Geschätzte Gesamtsumme",
                Items: "Positionen",
                Description: "Beschreibung",
                Qty: "Menge",
                Rate: "Einzelpreis",
                Amount: "Betrag",
                Subtotal: "Zwischensumme",
                Tax: "MwSt.",
                Total: "Gesamt",
                Notes: "Anmerkungen",
                Terms: "Allgemeine Geschäftsbedingungen",
                TaxVat: "USt-IdNr.",
                Registration: "HRB",
                ValidUntilFooter: "Dieses Angebot ist gültig bis {0}.",
                ThanksForConsidering: "Vielen Dank für Ihr Interesse an unseren Leistungen.",
                CreatedWith: "Erstellt mit Akinai"),

            [SupportedLanguage.Chinese] = new(
                CultureName: "zh-CN",
                Estimate: "报价单",
                StatusAccepted: "已接受",
                StatusRejected: "已拒绝",
                StatusConverted: "已转为发票",
                StatusExpired: "已过期",
                DaysAgo: "{0} 天前",
                StatusAwaiting: "等待回复",
                ValidUntilDate: "有效期至 {0}",
                StatusDraft: "草稿",
                DraftNotSent: "尚未发送",
                Date: "日期",
                ValidUntil: "有效期至",
                PreparedFor: "报价对象",
                EstimatedTotal: "预计总额",
                Items: "项目",
                Description: "说明",
                Qty: "数量",
                Rate: "单价",
                Amount: "金额",
                Subtotal: "小计",
                Tax: "税额",
                Total: "合计",
                Notes: "备注",
                Terms: "条款与条件",
                TaxVat: "税号",
                Registration: "注册号",
                ValidUntilFooter: "本报价单有效期至 {0}。",
                ThanksForConsidering: "感谢您考虑我们的服务。",
                CreatedWith: "由 Akinai 创建"),

            [SupportedLanguage.Vietnamese] = new(
                CultureName: "vi-VN",
                Estimate: "BÁO GIÁ",
                StatusAccepted: "Đã chấp nhận",
                StatusRejected: "Đã từ chối",
                StatusConverted: "Đã chuyển thành hóa đơn",
                StatusExpired: "Đã hết hạn",
                DaysAgo: "{0} ngày trước",
                StatusAwaiting: "Đang chờ phản hồi",
                ValidUntilDate: "Có hiệu lực đến {0}",
                StatusDraft: "Bản nháp",
                DraftNotSent: "Chưa gửi",
                Date: "Ngày",
                ValidUntil: "Có hiệu lực đến",
                PreparedFor: "Gửi tới",
                EstimatedTotal: "Tổng dự tính",
                Items: "Hạng mục",
                Description: "Mô tả",
                Qty: "SL",
                Rate: "Đơn giá",
                Amount: "Thành tiền",
                Subtotal: "Tạm tính",
                Tax: "Thuế",
                Total: "Tổng cộng",
                Notes: "Ghi chú",
                Terms: "Điều khoản và điều kiện",
                TaxVat: "Mã số thuế",
                Registration: "Số ĐKKD",
                ValidUntilFooter: "Báo giá này có hiệu lực đến {0}.",
                ThanksForConsidering: "Cảm ơn quý khách đã quan tâm đến dịch vụ của chúng tôi.",
                CreatedWith: "Được tạo bằng Akinai"),

            [SupportedLanguage.Russian] = new(
                CultureName: "ru-RU",
                Estimate: "КОММЕРЧЕСКОЕ ПРЕДЛОЖЕНИЕ",
                StatusAccepted: "Принято",
                StatusRejected: "Отклонено",
                StatusConverted: "Преобразовано в счёт",
                StatusExpired: "Срок истёк",
                DaysAgo: "{0} дней назад",
                StatusAwaiting: "Ожидает ответа",
                ValidUntilDate: "Действительно до {0}",
                StatusDraft: "Черновик",
                DraftNotSent: "Ещё не отправлено",
                Date: "Дата",
                ValidUntil: "Действительно до",
                PreparedFor: "Кому",
                EstimatedTotal: "Предварительная сумма",
                Items: "Позиции",
                Description: "Описание",
                Qty: "Кол-во",
                Rate: "Цена",
                Amount: "Сумма",
                Subtotal: "Промежуточный итог",
                Tax: "НДС",
                Total: "Итого",
                Notes: "Примечания",
                Terms: "Условия",
                TaxVat: "ИНН",
                Registration: "ОГРН",
                ValidUntilFooter: "Предложение действительно до {0}.",
                ThanksForConsidering: "Благодарим за интерес к нашим услугам.",
                CreatedWith: "Создано в Akinai"),

            [SupportedLanguage.Korean] = new(
                CultureName: "ko-KR",
                Estimate: "견적서",
                StatusAccepted: "승인됨",
                StatusRejected: "거절됨",
                StatusConverted: "청구서로 전환됨",
                StatusExpired: "기한 만료",
                DaysAgo: "{0}일 전",
                StatusAwaiting: "회신 대기 중",
                ValidUntilDate: "{0}까지 유효",
                StatusDraft: "임시 저장",
                DraftNotSent: "아직 발송되지 않음",
                Date: "작성일",
                ValidUntil: "유효 기한",
                PreparedFor: "수신처",
                EstimatedTotal: "견적 총액",
                Items: "항목",
                Description: "내용",
                Qty: "수량",
                Rate: "단가",
                Amount: "금액",
                Subtotal: "소계",
                Tax: "부가세",
                Total: "합계",
                Notes: "비고",
                Terms: "약관",
                TaxVat: "사업자등록번호",
                Registration: "법인등록번호",
                ValidUntilFooter: "본 견적서는 {0}까지 유효합니다.",
                ThanksForConsidering: "저희 서비스를 고려해 주셔서 감사합니다.",
                CreatedWith: "Akinai로 작성됨"),
        };

    /// <summary>Labels for a language, falling back to English.</summary>
    public static EstimateLabelSet For(SupportedLanguage language)
        => Sets.TryGetValue(language, out var set) ? set : English;

    /// <summary>
    /// Labels by language name, as stored on the document. An unrecognised or
    /// missing name is English rather than an error — see the note on
    /// <see cref="InvoiceLabels"/> about documents always having to render.
    /// </summary>
    public static EstimateLabelSet For(string? languageName)
        => !string.IsNullOrWhiteSpace(languageName)
           && Enum.TryParse<SupportedLanguage>(languageName, ignoreCase: true, out var language)
            ? For(language)
            : English;
}
