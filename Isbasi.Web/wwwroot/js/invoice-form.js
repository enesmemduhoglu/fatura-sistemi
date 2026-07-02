// Fatura formu: satır yönetimi + canlı toplam hesaplama.
// Hesaplama mantığı Data/InvoiceCalculator.cs ile birebir aynıdır.
(function () {
    var config = window.invoiceFormConfig;
    var $body = $('#linesBody');

    var FIELD_MAP = {
        'line-id': 'Id',
        'line-product-id': 'ProductId',
        'line-service-id': 'ServiceId',
        'line-item-name': 'ItemName',
        'line-qty': 'Quantity',
        'line-unit': 'Unit',
        'line-price': 'UnitPrice',
        'line-vat-rate': 'VatRate',
        'line-discount': 'DiscountValue',
        'line-discount-type': 'DiscountType'
    };

    function parseTr(value) {
        value = (value || '').toString().trim().replace(/\./g, '').replace(',', '.');
        var n = parseFloat(value);
        return isNaN(n) ? 0 : n;
    }

    function round2(n) { return Math.round((n + Number.EPSILON) * 100) / 100; }

    function fmt(n) {
        return n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function fmtInput(n) { return n.toFixed(2).replace('.', ','); }

    // ---- Firma bilgileri ----
    function fillFirm(firmId) {
        var firm = config.firms.find(function (f) { return f.id === parseInt(firmId); });
        $('#firmAddress').val(firm ? (firm.address || '') : '');
        $('#firmCountry').val(firm ? (firm.country || '') : '');
        $('#firmCity').val(firm ? (firm.city || '') : '');
        $('#firmDistrict').val(firm ? (firm.district || '') : '');
        $('#firmTaxOffice').val(firm ? (firm.taxOffice || '') : '');
        $('#firmTaxNumber').val(firm ? (firm.taxNumber || '') : '');
        $('#firmCorporate').prop('checked', firm && firm.kind === 0);
        $('#firmIndividual').prop('checked', firm && firm.kind === 1);
    }

    $('#firmSelect').select2({ placeholder: 'Firma Ünvanı / Adı Soyadı *', allowClear: true });
    $('#firmSelect').on('change', function () { fillFirm($(this).val()); });
    if (config.selectedFirmId) fillFirm(config.selectedFirmId);

    // ---- Vade günü butonları ----
    $('.due-buttons .btn').on('click', function () {
        var days = parseInt($(this).data('days'));
        var invoiceDate = $('#InvoiceDate').val();
        var base = invoiceDate ? new Date(invoiceDate) : new Date();
        base.setDate(base.getDate() + days);
        $('#dueDateInput').val(base.toISOString().slice(0, 10));
        $('.due-buttons .btn').removeClass('selected');
        $(this).addClass('selected');
    });

    // ---- Satır yönetimi ----
    function initItemSelect($row) {
        var $select = $row.find('.item-select');
        var currentName = $row.find('.line-item-name').val();
        var productId = $row.find('.line-product-id').val();
        var serviceId = $row.find('.line-service-id').val();
        var currentKey = productId ? 'p' + productId : (serviceId ? 's' + serviceId : null);

        config.items.forEach(function (item) {
            $select.append(new Option(item.name, item.key, false, item.key === currentKey));
        });
        if (!currentKey && currentName) {
            $select.append(new Option(currentName, currentName, true, true));
        }
        if (!currentKey && !currentName) $select.val(null);

        $select.select2({
            placeholder: 'Ürün ya da hizmet seçin',
            tags: true,
            createTag: function (params) {
                var term = $.trim(params.term);
                if (term === '') return null;
                return { id: term, text: term, newTag: true };
            }
        });

        $select.on('change', function () {
            var key = $select.val();
            var item = config.items.find(function (i) { return i.key === key; });
            if (item) {
                $row.find('.line-product-id').val(item.productId || '');
                $row.find('.line-service-id').val(item.serviceId || '');
                $row.find('.line-item-name').val(item.name);
                $row.find('.line-unit').val(item.unit);
                $row.find('.line-price').val(fmtInput(item.price));
                $row.find('.line-vat-rate').val(item.vatRate.toString());
            } else {
                // serbest metin girişi
                $row.find('.line-product-id').val('');
                $row.find('.line-service-id').val('');
                $row.find('.line-item-name').val(key || '');
            }
            recalc();
        });
    }

    function reindexRows() {
        $body.find('tr.line-row').each(function (i) {
            var $row = $(this);
            $row.find('.line-no').text(i + 1);
            Object.keys(FIELD_MAP).forEach(function (cls) {
                $row.find('.' + cls).attr('name', 'Lines[' + i + '].' + FIELD_MAP[cls]);
            });
        });
    }

    function addRow() {
        var template = document.getElementById('lineTemplate');
        var $row = $(template.content.firstElementChild.cloneNode(true));
        $body.append($row);
        initItemSelect($row);
        reindexRows();
        recalc();
    }

    $('#addLineBtn').on('click', addRow);

    $body.on('click', '.remove-line', function () {
        $(this).closest('tr').remove();
        reindexRows();
        recalc();
    });

    // ---- Toplam hesaplama (InvoiceCalculator.cs ile aynı) ----
    function recalc() {
        var vatIncluded = config.vatIncluded;
        var subTotal = 0, lineDiscountTotal = 0, vatSum = 0;

        $body.find('tr.line-row').each(function () {
            var $row = $(this);
            var qty = parseTr($row.find('.line-qty').val());
            var price = parseTr($row.find('.line-price').val());
            var rate = parseFloat($row.find('.line-vat-rate').val()) || 0;
            var discountValue = parseTr($row.find('.line-discount').val());
            var discountType = $row.find('.line-discount-type').val();

            var gross = round2(qty * price);
            var discount = discountType === 'Rate' ? round2(gross * discountValue / 100) : discountValue;
            var net = gross - discount;

            var base, vat;
            if (vatIncluded) {
                base = round2(net / (1 + rate / 100));
                vat = round2(net - base);
                discount = round2(discount / (1 + rate / 100));
            } else {
                base = net;
                vat = round2(net * rate / 100);
            }

            $row.find('.line-vat-amount').text(fmt(vat));
            $row.find('.line-total').text(fmt(base));

            subTotal += base;
            lineDiscountTotal += discount;
            vatSum += vat;
        });

        var generalValue = parseTr($('#generalDiscount').val());
        var generalType = $('#generalDiscountType').val();
        var generalDiscount = generalType === 'Rate' ? round2(subTotal * generalValue / 100) : generalValue;
        if (generalDiscount > subTotal) generalDiscount = subTotal;

        var ratio = subTotal > 0 ? (subTotal - generalDiscount) / subTotal : 1;
        var vatTotal = round2(vatSum * ratio);
        var total = round2(subTotal - generalDiscount);

        $('#totalSub').text(fmt(round2(subTotal)) + ' ₺');
        $('#totalDiscount').text(fmt(round2(lineDiscountTotal + generalDiscount)) + ' ₺');
        $('#totalNet').text(fmt(total) + ' ₺');
        $('#totalVat').text(fmt(vatTotal) + ' ₺');
        $('#totalGrand').text(fmt(round2(total + vatTotal)) + ' ₺');
    }

    $body.on('input change', 'input, select', recalc);
    $('#generalDiscount, #generalDiscountType').on('input change', recalc);

    // Mevcut satırların select2'lerini kur, hiç satır yoksa boş satır ekle
    $body.find('tr.line-row').each(function () { initItemSelect($(this)); });
    if ($body.find('tr.line-row').length === 0) addRow();
    reindexRows();
    recalc();
})();
