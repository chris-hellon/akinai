using System.Globalization;
using Akinai.Shared.Enums;

namespace Akinai.Shared.Dunning;

/// <summary>
/// One ready-to-send message: a subject line and a body.
///
/// Neither carries a greeting line naming the client, nor a signature. Both are
/// the mail layer's job, which is the only place that knows the client's contact
/// name and the user's own sign-off — and a template that guessed at either would
/// produce "Dear ," on the one occasion it mattered most.
/// </summary>
public sealed record DunningMessage(string Subject, string Body);

/// <summary>
/// Every rung of the ladder, written in one language.
///
/// <para>
/// Placeholders are positional and the order is fixed for every language and every
/// stage: <c>{0}</c> invoice number, <c>{1}</c> amount (already formatted, with its
/// currency symbol), <c>{2}</c> due date (already formatted for this culture),
/// <c>{3}</c> whole days past due. A template may ignore any of them — the advance
/// notice has nothing to say about days overdue — but it must never reorder them.
/// </para>
/// </summary>
public sealed record DunningTemplateSet(
    string CultureName,
    DunningMessage AdvanceNotice,
    DunningMessage GentleReminder,
    DunningMessage FirmReminder,
    DunningMessage FinalNotice)
{
    /// <summary>
    /// The culture to format this message's dates and numbers with. Falls back to
    /// invariant rather than throwing, for the same reason the invoice labels do:
    /// on WebAssembly the ICU data for an arbitrary culture may not have been
    /// loaded, and an unfamiliar date order beats a chase that never goes out.
    /// </summary>
    public CultureInfo Culture
    {
        get
        {
            try { return CultureInfo.GetCultureInfo(CultureName); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }
    }

    /// <summary>
    /// The message for a stage. An unmapped stage is a programming error rather
    /// than bad data — the enum is closed and this record has a slot for every
    /// member — so this throws instead of silently sending the wrong rung.
    /// </summary>
    public DunningMessage For(DunningStage stage) => stage switch
    {
        DunningStage.AdvanceNotice => AdvanceNotice,
        DunningStage.GentleReminder => GentleReminder,
        DunningStage.FirmReminder => FirmReminder,
        DunningStage.FinalNotice => FinalNotice,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "No dunning template for this stage."),
    };
}

/// <summary>
/// What a chasing email says, per stage, per language.
///
/// <para>
/// Compiled tables rather than machine translation, for the reasons the invoice
/// labels give — they never change, they must work when the translation API is
/// down — plus one that matters more here. <b>The hard part of this file is not
/// translation, it is register.</b> The same escalation, translated faithfully,
/// is read as businesslike in Berlin, cold in Hanoi and faintly threatening in
/// Seoul. An engine that renders the English sentence correctly in Korean will
/// still cost the user the relationship.
/// </para>
///
/// <para>
/// So each language block below is pitched for its own market: how direct the ask
/// is, how much of the blame is absorbed by the sender, whether a deadline is
/// stated outright, and whether the sender is "I" or "we". The comment on each
/// block says what was chosen and why — and, where the author was not confident,
/// says that too, because a wrong guess that is documented can be corrected by
/// the first native speaker who reads it, and an undocumented one cannot.
/// </para>
///
/// <para>
/// Non-negotiable across all eight: the advance notice is never a chase, no stage
/// accuses the client of bad faith, every stage offers a way to reply other than
/// paying, and the final notice states a concrete consequence. Escalation that
/// never arrives anywhere teaches a client that the reminders can be ignored.
/// </para>
/// </summary>
public static class DunningTemplates
{
    // English (en-GB). Understated and first-person singular, because the typical
    // Akinai user is one person, and "we" from a sole trader reads as a hiding
    // place. The escalation is carried by specificity rather than volume — the
    // gentle stage offers an excuse ("these things slip"), the firm stage asks for
    // a date, the final stage names a deadline and a consequence. British
    // indirectness has a failure mode in exactly that last step: a final notice
    // that is still hedging is read as a bluff, so the hedging stops there.
    private static readonly DunningTemplateSet English = new(
        CultureName: "en-GB",
        AdvanceNotice: new(
            Subject: "Invoice {0} falls due on {2}",
            Body: """
                  A quick note that invoice {0} for {1} falls due on {2}.

                  If it is already scheduled for payment, please ignore this. If anything on the invoice needs correcting, now is a good time to tell me and I will send a revised copy.

                  Many thanks,
                  """),
        GentleReminder: new(
            Subject: "Invoice {0} is now overdue",
            Body: """
                  Invoice {0} for {1} was due on {2} and is now {3} days past due. These things slip, so this may simply have been missed.

                  If it has already been paid, let me know and I will check at my end. Otherwise, I would be grateful if you could arrange payment this week.

                  Many thanks,
                  """),
        FirmReminder: new(
            Subject: "Overdue: invoice {0}, {3} days past due",
            Body: """
                  Invoice {0} for {1} was due on {2} and is now {3} days overdue. I have not had a reply to my earlier reminder.

                  Please either send payment or tell me the date you expect to pay. If something is holding it up at your end, I would much rather know than keep writing.

                  Many thanks,
                  """),
        FinalNotice: new(
            Subject: "Final notice: invoice {0}",
            Body: """
                  This is my final reminder about invoice {0} for {1}, which was due on {2} and is now {3} days overdue.

                  If payment does not reach me within seven days, and I do not hear from you with a date, I will stop sending reminders and pass the account on for recovery. I would far rather settle it between us.

                  Many thanks,
                  """));

    private static readonly IReadOnlyDictionary<SupportedLanguage, DunningTemplateSet> Sets =
        new Dictionary<SupportedLanguage, DunningTemplateSet>
        {
            [SupportedLanguage.English] = English,

            // Spanish (es-ES). Formal "usted" throughout, and noticeably more
            // courtesy furniture than the English — "le agradecería", "le ruego" —
            // which in Spanish business writing is the normal baseline rather than
            // obsequiousness; stripping it to English brevity reads as brusque.
            // Blame is kept off the client until the firm stage, then the ask
            // becomes plainly direct, which Spanish correspondence takes in its
            // stride. The final notice says "reclamación de la deuda" rather than
            // naming any legal instrument, since the user may have no contract
            // behind the threat.
            //
            // Uncertain: pitched at Peninsular Spanish. Latin American business
            // registers differ (Mexican correspondence is generally warmer and
            // more deferential, Argentine more direct), and "le ruego" can read as
            // archaic outside Spain. Worth a second opinion per market.
            [SupportedLanguage.Spanish] = new(
                CultureName: "es-ES",
                AdvanceNotice: new(
                    Subject: "La factura {0} vence el {2}",
                    Body: """
                          Le escribo únicamente para recordarle que la factura {0}, por un importe de {1}, vence el {2}.

                          Si el pago ya está programado, haga caso omiso de este mensaje. Si observa algo que deba corregirse, le agradecería que me lo indicara y le enviaré una factura rectificada.

                          Un cordial saludo,
                          """),
                GentleReminder: new(
                    Subject: "Recordatorio de pago: factura {0}",
                    Body: """
                          La factura {0}, por un importe de {1}, venció el {2} y acumula {3} días de retraso. Entiendo que puede tratarse de un simple olvido.

                          Si el pago ya se ha realizado, le ruego que me lo confirme y lo comprobaré por mi parte. En caso contrario, le agradecería que gestionara el abono esta semana.

                          Un cordial saludo,
                          """),
                FirmReminder: new(
                    Subject: "Factura {0} pendiente: {3} días de retraso",
                    Body: """
                          La factura {0}, por un importe de {1}, venció el {2} y lleva {3} días pendiente de pago. No he recibido respuesta a mi recordatorio anterior.

                          Le pido que efectúe el pago o que me indique una fecha concreta en la que vaya a realizarlo. Si existe algún impedimento por su parte, preferiría conocerlo a seguir escribiéndole.

                          Atentamente,
                          """),
                FinalNotice: new(
                    Subject: "Último aviso: factura {0}",
                    Body: """
                          Este es el último aviso relativo a la factura {0}, por un importe de {1}, con vencimiento el {2} y {3} días de retraso.

                          Si en el plazo de siete días no recibo el pago ni una fecha firme de abono, daré por concluidas las gestiones amistosas e iniciaré la reclamación de la deuda. Confío en que podamos resolverlo antes de llegar a ese punto.

                          Atentamente,
                          """)),

            // French (fr-FR). The most formally structured of the eight: fixed
            // epistolary openings and closings, and the commercial "nous" even for
            // a sole trader — in French business correspondence "je" from a supplier
            // reads as an individual complaining rather than a business invoicing.
            // Blame is deflected grammatically rather than socially: "sauf erreur ou
            // omission de notre part" puts the possibility of our own mistake first,
            // which is the conventional French way to say "you have not paid"
            // without the accusation landing.
            //
            // Deliberately avoided: "mise en demeure", which in France is a formal
            // legal act with specific requirements and consequences. The final
            // notice points at it ("dernier rappel") without claiming to be one.
            // Late-payment penalties are referenced as arising from the user's own
            // terms of sale, not from statute — the statutory indemnity exists, but
            // an automated email is the wrong place to assert a figure we cannot
            // verify applies to this contract.
            [SupportedLanguage.French] = new(
                CultureName: "fr-FR",
                AdvanceNotice: new(
                    Subject: "Facture {0} — échéance le {2}",
                    Body: """
                          Madame, Monsieur,

                          Nous vous informons que la facture {0}, d'un montant de {1}, arrive à échéance le {2}.

                          Si son règlement est déjà programmé, veuillez ne pas tenir compte de ce message. Si une correction s'avérait nécessaire, nous vous remercions de nous le faire savoir et nous vous adresserons une facture rectificative.

                          Nous vous prions d'agréer, Madame, Monsieur, l'expression de nos salutations distinguées.
                          """),
                GentleReminder: new(
                    Subject: "Rappel : facture {0} échue",
                    Body: """
                          Madame, Monsieur,

                          Sauf erreur ou omission de notre part, la facture {0}, d'un montant de {1}, échue le {2}, demeure impayée à ce jour, soit un retard de {3} jours.

                          Nous vous serions reconnaissants de bien vouloir procéder à son règlement. Si le paiement a déjà été effectué, nous vous remercions de nous le signaler afin que nous procédions aux vérifications nécessaires.

                          Nous vous prions d'agréer, Madame, Monsieur, l'expression de nos salutations distinguées.
                          """),
                FirmReminder: new(
                    Subject: "Relance : facture {0} impayée depuis {3} jours",
                    Body: """
                          Madame, Monsieur,

                          Malgré notre précédent rappel, la facture {0}, d'un montant de {1}, échue le {2}, reste impayée, soit un retard de {3} jours.

                          Nous vous demandons de bien vouloir procéder au règlement sans délai, ou de nous communiquer la date à laquelle celui-ci sera effectué. Nous vous rappelons que tout retard de paiement est susceptible de donner lieu aux pénalités prévues par nos conditions de vente.

                          Nous vous prions d'agréer, Madame, Monsieur, l'expression de nos salutations distinguées.
                          """),
                FinalNotice: new(
                    Subject: "Dernier rappel avant recouvrement — facture {0}",
                    Body: """
                          Madame, Monsieur,

                          Ce message constitue notre dernier rappel concernant la facture {0}, d'un montant de {1}, échue le {2} et présentant un retard de {3} jours.

                          À défaut de règlement ou d'un engagement de paiement de votre part dans un délai de sept jours, nous serons contraints de confier ce dossier au recouvrement. Nous préférerions de loin régler cette question directement avec vous.

                          Nous vous prions d'agréer, Madame, Monsieur, l'expression de nos salutations distinguées.
                          """)),

            // German (de-DE). Directness is the courtesy here: no small talk, no
            // softening of the ask, facts and a concrete deadline from the start.
            // Vagueness is what reads as unprofessional to a German client, not
            // firmness, so this block states the figures and the date and stops.
            //
            // The stage names follow the ladder German clients already recognise —
            // Zahlungserinnerung (a reminder, no implication of fault), then
            // Mahnung, then letzte Mahnung. That sequence is culturally expected
            // before anything legal happens, and skipping a rung is what would be
            // read as aggressive. Deliberately absent: Mahngebühren and
            // Verzugszinsen. Both are commonly asserted in German reminders and
            // both depend on the contract and on Verzug having actually begun, so
            // this file does not claim them on the user's behalf.
            [SupportedLanguage.German] = new(
                CultureName: "de-DE",
                AdvanceNotice: new(
                    Subject: "Rechnung {0} — fällig am {2}",
                    Body: """
                          Sehr geehrte Damen und Herren,

                          die Rechnung {0} über {1} ist am {2} zur Zahlung fällig.

                          Sollte die Zahlung bereits veranlasst sein, betrachten Sie diese Nachricht als erledigt. Falls an der Rechnung etwas zu korrigieren ist, teilen Sie uns dies bitte mit; wir stellen sie umgehend neu aus.

                          Mit freundlichen Grüßen
                          """),
                GentleReminder: new(
                    Subject: "Zahlungserinnerung: Rechnung {0}",
                    Body: """
                          Sehr geehrte Damen und Herren,

                          die Rechnung {0} über {1} war am {2} fällig und ist seit {3} Tagen offen. Vermutlich ist sie übersehen worden.

                          Wir bitten Sie, den Betrag innerhalb der nächsten sieben Tage zu überweisen. Sollte die Zahlung bereits erfolgt sein, betrachten Sie diese Erinnerung als gegenstandslos und sagen Sie uns kurz Bescheid.

                          Mit freundlichen Grüßen
                          """),
                FirmReminder: new(
                    Subject: "Mahnung: Rechnung {0} seit {3} Tagen offen",
                    Body: """
                          Sehr geehrte Damen und Herren,

                          trotz unserer Zahlungserinnerung ist die Rechnung {0} über {1} weiterhin offen. Sie war am {2} fällig; der Zahlungsverzug beträgt {3} Tage.

                          Wir bitten Sie, den offenen Betrag unverzüglich zu überweisen oder uns verbindlich mitzuteilen, zu welchem Termin die Zahlung erfolgt. Falls Unterlagen von uns fehlen, nennen Sie sie uns — wir reichen sie noch am selben Tag nach.

                          Mit freundlichen Grüßen
                          """),
                FinalNotice: new(
                    Subject: "Letzte Mahnung: Rechnung {0}",
                    Body: """
                          Sehr geehrte Damen und Herren,

                          dies ist unsere letzte Mahnung zur Rechnung {0} über {1}, fällig am {2} und seit {3} Tagen überfällig.

                          Geht die Zahlung nicht innerhalb von sieben Tagen bei uns ein und erhalten wir keine verbindliche Zahlungszusage, geben wir die Forderung zur weiteren Verfolgung ab. Wir würden die Angelegenheit lieber direkt mit Ihnen klären.

                          Mit freundlichen Grüßen
                          """)),

            // Chinese (zh-CN, Simplified). Face is the organising constraint. Every
            // stage attributes non-payment to a process rather than to a person
            // ("可能是财务流程中的疏漏"), and asks the recipient to *check* (核实 /
            // 查询) rather than to explain themselves — which lets whoever reads it
            // act without having to admit anything. 您 throughout, and the ask is
            // framed as 协助 ("assist us"), which keeps the relationship above the
            // transaction even while the amount and the day count are stated plainly.
            //
            // The final notice is the delicate one. It still names a deadline and a
            // consequence, because a message with no consequence is not a final
            // notice — but the consequence is phrased obliquely (通过其他途径处理)
            // and followed by a return to cooperation, since a bare threat is a
            // relationship-ending move in a way it is not in German.
            //
            // Uncertain, and worth a native reader: 以免影响双方今后的合作 ("so as not
            // to affect our future cooperation") is a conventional closing in
            // Chinese collections writing, but it sits close to a veiled threat and
            // how it lands depends on the existing relationship. Also pitched for
            // mainland practice; Taiwan and Hong Kong norms (and Traditional script)
            // differ, and this table has one Chinese entry for all of them.
            [SupportedLanguage.Chinese] = new(
                CultureName: "zh-CN",
                AdvanceNotice: new(
                    Subject: "发票 {0} 将于 {2} 到期",
                    Body: """
                          您好：

                          发票 {0}（金额 {1}）将于 {2} 到期，特此提醒。

                          如您已安排付款，请忽略本邮件。若发票内容需要调整，也请随时告知，我们会尽快重新开具。

                          顺祝商祺
                          """),
                GentleReminder: new(
                    Subject: "发票 {0} 付款提醒",
                    Body: """
                          您好：

                          发票 {0}（金额 {1}）的付款期限为 {2}，目前已逾期 {3} 天。可能是财务流程中的疏漏，烦请协助查询一下。

                          如款项已经支付，请告知我们，我们再核对入账情况。如尚未安排，烦请协助在本周内处理。感谢您的配合。

                          顺祝商祺
                          """),
                FirmReminder: new(
                    Subject: "关于发票 {0} 逾期 {3} 天的付款事宜",
                    Body: """
                          您好：

                          发票 {0}（金额 {1}）应于 {2} 前付清，目前已逾期 {3} 天，此前的提醒尚未收到回复。

                          烦请安排付款，或告知我们预计付款的具体日期，以便我们安排账务。如流程上需要我们提供补充资料，也请一并告知，我们当天即可提供。

                          顺祝商祺
                          """),
                FinalNotice: new(
                    Subject: "发票 {0} 最后一次付款提醒",
                    Body: """
                          您好：

                          这是我们就发票 {0}（金额 {1}）发出的最后一次提醒。该发票应于 {2} 前付清，目前已逾期 {3} 天。

                          如七天内仍未收到款项或付款安排的回复，我们将不得不通过其他途径处理此笔欠款。我们仍希望能与您直接解决，以免影响双方今后的合作。

                          顺祝商祺
                          """)),

            // Vietnamese (vi-VN). Formal and warm at once: "Kính gửi Quý khách"
            // throughout, which is deferential in a way that would read as servile
            // in German but is simply correct here, and a closing that thanks the
            // client even while chasing them. Like the Chinese block, fault is
            // placed on the process ("bị bỏ sót trong quá trình xử lý") rather than
            // the person, and the firm stage asks for a date rather than issuing a
            // demand.
            //
            // Uncertain: Vietnamese business correspondence is noticeably warmer and
            // more hierarchical than any of the European registers here, and the
            // right level of deference depends on relative seniority and on how
            // established the relationship is — neither of which this file knows.
            // The final notice is therefore the gentlest of the eight that still
            // names a deadline; a Vietnamese reader should check whether it is firm
            // enough to be taken as final, which is the opposite of the risk the
            // German block runs.
            [SupportedLanguage.Vietnamese] = new(
                CultureName: "vi-VN",
                AdvanceNotice: new(
                    Subject: "Hóa đơn {0} đến hạn ngày {2}",
                    Body: """
                          Kính gửi Quý khách,

                          Chúng tôi xin thông báo hóa đơn {0} với số tiền {1} sẽ đến hạn thanh toán vào ngày {2}.

                          Nếu Quý khách đã lên lịch thanh toán, xin vui lòng bỏ qua email này. Nếu có nội dung nào cần điều chỉnh, xin Quý khách thông báo để chúng tôi phát hành lại hóa đơn.

                          Trân trọng cảm ơn Quý khách.
                          """),
                GentleReminder: new(
                    Subject: "Nhắc thanh toán hóa đơn {0}",
                    Body: """
                          Kính gửi Quý khách,

                          Hóa đơn {0} với số tiền {1} có hạn thanh toán là ngày {2} và hiện đã quá hạn {3} ngày. Có lẽ hóa đơn đã bị bỏ sót trong quá trình xử lý.

                          Nếu Quý khách đã thanh toán, xin vui lòng thông báo để chúng tôi đối chiếu lại. Nếu chưa, kính mong Quý khách thu xếp thanh toán trong tuần này.

                          Trân trọng cảm ơn Quý khách.
                          """),
                FirmReminder: new(
                    Subject: "Hóa đơn {0} đã quá hạn {3} ngày",
                    Body: """
                          Kính gửi Quý khách,

                          Hóa đơn {0} với số tiền {1} đến hạn ngày {2} và hiện đã quá hạn {3} ngày. Chúng tôi chưa nhận được phản hồi cho thư nhắc trước đó.

                          Kính mong Quý khách thanh toán hoặc cho chúng tôi biết ngày dự kiến thanh toán. Nếu có vướng mắc nào cần chúng tôi phối hợp giải quyết, xin Quý khách cho biết.

                          Trân trọng cảm ơn Quý khách.
                          """),
                FinalNotice: new(
                    Subject: "Thông báo cuối cùng về hóa đơn {0}",
                    Body: """
                          Kính gửi Quý khách,

                          Đây là thư nhắc cuối cùng của chúng tôi về hóa đơn {0} với số tiền {1}, đến hạn ngày {2} và đã quá hạn {3} ngày.

                          Nếu trong vòng bảy ngày tới chúng tôi không nhận được khoản thanh toán hoặc cam kết về ngày thanh toán, chúng tôi sẽ phải chuyển việc thu hồi khoản nợ này sang hình thức khác. Chúng tôi rất mong được giải quyết trực tiếp cùng Quý khách.

                          Trân trọng.
                          """)),

            // Russian (ru-RU). Dry, factual and procedural — the register of a
            // supplier's accounts department, not of a person asking a favour.
            // Hedging is the real mistake here: a softly worded Russian reminder is
            // read as one that can be deprioritised, so the early stages still state
            // the figure and the day count flatly and ask which date the payment is
            // scheduled for.
            //
            // The opening shifts with the stage — "Добрый день!" while this is
            // routine, the more formal "Уважаемые господа!" once it is not, which is
            // itself part of the escalation and is noticed. The final notice names
            // взыскание задолженности "в установленном порядке" without claiming a
            // specific legal step, since we do not know whether the user has a
            // contract or a claims procedure behind it. Offering to resend documents
            // is not filler: a Russian client genuinely may be unable to pay without
            // matching paperwork, and the offer removes the most common real blocker.
            [SupportedLanguage.Russian] = new(
                CultureName: "ru-RU",
                AdvanceNotice: new(
                    Subject: "Счёт {0}: срок оплаты {2}",
                    Body: """
                          Добрый день!

                          Напоминаем, что срок оплаты счёта {0} на сумму {1} — {2}.

                          Если платёж уже поставлен в очередь, просто проигнорируйте это письмо. Если в счёте необходимо что-то исправить, сообщите нам, и мы оперативно направим исправленный документ.

                          С уважением,
                          """),
                GentleReminder: new(
                    Subject: "Напоминание об оплате счёта {0}",
                    Body: """
                          Добрый день!

                          Счёт {0} на сумму {1} должен был быть оплачен {2}; просрочка составляет {3} дн. Вероятно, счёт просто не дошёл до оплаты.

                          Просим сообщить, на какую дату поставлен платёж. Если оплата уже произведена, пришлите, пожалуйста, платёжное поручение — мы проверим поступление у себя.

                          С уважением,
                          """),
                FirmReminder: new(
                    Subject: "Счёт {0}: просрочка {3} дн.",
                    Body: """
                          Уважаемые господа!

                          Счёт {0} на сумму {1} со сроком оплаты {2} до настоящего времени не оплачен. Просрочка составляет {3} дн. Ответа на наше предыдущее письмо мы не получили.

                          Просим произвести оплату или письменно подтвердить дату платежа. Если для оплаты требуются дополнительные документы с нашей стороны, сообщите об этом — мы направим их в тот же день.

                          С уважением,
                          """),
                FinalNotice: new(
                    Subject: "Счёт {0}: последнее напоминание перед взысканием",
                    Body: """
                          Уважаемые господа!

                          Настоящее письмо является последним напоминанием об оплате счёта {0} на сумму {1} со сроком оплаты {2}. Просрочка составляет {3} дн.

                          Если в течение семи дней оплата не поступит и мы не получим письменного подтверждения даты платежа, мы будем вынуждены перейти к взысканию задолженности в установленном порядке. Мы по-прежнему предпочли бы урегулировать вопрос напрямую.

                          С уважением,
                          """)),

            // Korean (ko-KR). The creditor apologises for the inconvenience of
            // asking — "번거롭게 해드려 죄송합니다" — which looks absurd beside the
            // German block and is simply how this is done: the apology is what makes
            // the request askable without either side losing face. Formal 합니다
            // style throughout, and the ask is always for a *confirmation* (입금 확인
            // 요청, 지급 예정일 안내) rather than for the money, which gives the
            // recipient something to do that is not an admission.
            //
            // Subjects carry bracketed tags ([안내], [요청], [최종 안내]), which is
            // normal in Korean business email and does the escalation work before
            // the message is even opened. The final notice stays softened in form
            // while being unambiguous in content — "부득이 다른 절차를 통해" — because
            // a blunt demand costs disproportionately more here than the few days of
            // ambiguity it would save.
            //
            // Uncertain: written for a company recipient. Korean distinguishes
            // addressing a company (귀사) from an individual, and the honorific level
            // shifts with it; this block avoids 귀사 so it does not read oddly to a
            // sole trader, at the cost of being slightly less formal than a
            // corporate accounts department might expect. Also unverified: whether
            // the seven-day deadline should be stated as plainly as it is, or
            // referred to a phone call first, which is the more usual Korean
            // escalation path.
            [SupportedLanguage.Korean] = new(
                CultureName: "ko-KR",
                AdvanceNotice: new(
                    Subject: "[안내] {0} 청구서 지급기일 {2}",
                    Body: """
                          안녕하세요.

                          {0} 청구서(금액 {1})의 지급기일이 {2}임을 안내드립니다.

                          이미 지급 예정으로 처리되어 있다면 확인만 부탁드립니다. 청구 내용 중 수정이 필요한 부분이 있으면 말씀해 주시면 신속히 반영하여 다시 보내드리겠습니다.

                          감사합니다.
                          """),
                GentleReminder: new(
                    Subject: "[안내] {0} 청구서 입금 확인 요청",
                    Body: """
                          안녕하세요.

                          번거롭게 해드려 죄송합니다. {0} 청구서(금액 {1})의 지급기일이 {2}였으며 현재 {3}일이 경과하였습니다. 내부 지급 절차에서 누락된 것은 아닌지 확인 부탁드립니다.

                          이미 입금하셨다면 알려주시면 저희 쪽에서 다시 확인하겠습니다. 아직이라면 금주 중에 처리해 주시면 감사하겠습니다.

                          감사합니다.
                          """),
                FirmReminder: new(
                    Subject: "[요청] {0} 청구서 미입금 ({3}일 경과)",
                    Body: """
                          안녕하세요.

                          {0} 청구서(금액 {1})의 지급기일 {2}이 지나 현재 {3}일이 경과하였으나 입금이 확인되지 않았습니다. 앞서 보낸 안내에도 회신을 받지 못하였습니다.

                          입금을 진행해 주시거나, 가능한 지급 예정일을 알려주시면 감사하겠습니다. 저희 쪽에서 추가로 제출해야 할 서류가 있다면 말씀해 주십시오. 당일 보내드리겠습니다.

                          감사합니다.
                          """),
                FinalNotice: new(
                    Subject: "[최종 안내] {0} 청구서 미입금",
                    Body: """
                          안녕하세요.

                          {0} 청구서(금액 {1}, 지급기일 {2})에 대한 최종 안내입니다. 현재 {3}일이 경과하였습니다.

                          앞으로 7일 이내에 입금 또는 지급 예정일에 대한 회신을 받지 못할 경우, 부득이 다른 절차를 통해 대금을 청구할 수밖에 없습니다. 가능하다면 직접 협의하여 마무리하고 싶습니다.

                          감사합니다.
                          """)),
        };

    /// <summary>Templates for a language, falling back to English for anything unmapped.</summary>
    public static DunningTemplateSet For(SupportedLanguage language)
        => Sets.TryGetValue(language, out var set) ? set : English;

    /// <summary>
    /// Templates for a language name as it crosses the wire (the enum travels as a
    /// string, and the language is pinned to the invoice). Unrecognised names fall
    /// back to English rather than throwing — a stale value must not stop a client
    /// being chased, even if it chases them in the wrong language.
    /// </summary>
    public static DunningTemplateSet For(string? languageName)
        => Enum.TryParse<SupportedLanguage>(languageName, ignoreCase: true, out var parsed)
            ? For(parsed)
            : English;

    /// <summary>
    /// Whether this language is genuinely written out above, as opposed to being
    /// served English by <see cref="For(SupportedLanguage)"/>.
    ///
    /// Exists because the fallback is a safety net that can hide a hole: a new
    /// language added to <see cref="SupportedLanguage"/> and forgotten here would
    /// quietly chase every client in that market in English, and nothing would
    /// fail. Tests assert against this rather than against the fallback.
    /// </summary>
    public static bool HasTemplates(SupportedLanguage language) => Sets.ContainsKey(language);

    /// <summary>
    /// A stage's message with the placeholders filled in.
    /// </summary>
    /// <param name="amount">
    /// The amount, already formatted with its currency. Formatting money is the
    /// caller's job: the currency is the invoice's, not the language's, and a
    /// Korean-language invoice billed in euros must not acquire a won symbol here.
    /// </param>
    /// <param name="dueDate">The due date, already formatted. Usually via <see cref="DunningTemplateSet.Culture"/>.</param>
    /// <param name="daysPastDue">
    /// Whole days past due. Clamped at zero, because the advance notice runs before
    /// the due date and no template should ever be able to say "-3 days overdue".
    /// </param>
    public static DunningMessage Render(
        SupportedLanguage language,
        DunningStage stage,
        string invoiceNumber,
        string amount,
        string dueDate,
        int daysPastDue)
    {
        var set = For(language);
        var template = set.For(stage);
        var days = Math.Max(0, daysPastDue);

        return new DunningMessage(
            string.Format(set.Culture, template.Subject, invoiceNumber, amount, dueDate, days),
            string.Format(set.Culture, template.Body, invoiceNumber, amount, dueDate, days));
    }
}
