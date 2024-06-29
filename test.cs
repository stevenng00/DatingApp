string invoiceID = HttpContext.Current.Request.QueryString["Invoice_ID"].ToString();

int[] selrows = UCAControls.SelectorUtility.getGridViewSelectedRowIndics(sgv);
if (selrows == null && dRow != null)
    selrows = new int[] { 0 };

string sql = @"

DECLARE @invoiceID int;
DECLARE @qty decimal(20,0);
DECLARE @serviceID int;
DECLARE @detailID int;
DECLARE @invItemID int;

SET @invoiceID = '" + invoiceID + @"';

";

foreach (int rno in selrows)
{
    string detailID = (dRow != null) ? dRow["Service_Confirmation_Detail_ID"].ToString() : UCA.CustomButton.CustomUtility.Get_GridViewValue("Service_Confirmation_Detail_ID", sgv.Rows[rno]);
    string qty = (dRow != null) ? dRow["Available_Invoice"].ToString() : UCA.CustomButton.CustomUtility.Get_GridViewValue("Available_Invoice", sgv.Rows[rno]);

    if (string.IsNullOrEmpty(qty))
        qty = "0";

    sql += @"

    SET @detailID = '" + detailID + @"'
    SET @qty = " + qty + @"
    SET @invItemID = (SELECT Invoice_Item_ID FROM Invoice_Item WHERE Service_Confirmation_Detail_ID = @detailID AND Invoice_ID = @invoiceID)


    IF (@invItemID IS NULL)
        BEGIN
            INSERT INTO Invoice_Item
            (
                Creation_Time,
                Creator,
                Invoice_ID,
                Service_Confirmation_Detail_ID,
                Sq_Meter,
                Quantity,
                Adjustment,
                Total_Sq_Meter,
                Description,
                Origin_Service_ID,
                Unit_Price,
                Amount,
                AS_Profile_ID
            )
            (
                SELECT
                    GETDATE(),
                    '" + UserID + @"',
                    @invoiceID,
                    detail.Service_Confirmation_Detail_ID,
                    CASE 
                        WHEN (priceTable.Charge_Type = 'Sq Meter') THEN
                            CASE 
                                WHEN (detail.Sq_Meter < ISNULL(detail.Min_Charge_Sq_Meter,0)) THEN 
                                    ISNULL(detail.Min_Charge_Sq_Meter,0) 
                                ELSE 
                                    detail.Sq_Meter 
                            END
                        ELSE
                            NULL
                    END,
                    @qty,
                    detail.Adjustment,
                    CASE 
                        WHEN (priceTable.Charge_Type = 'Sq Meter') THEN
                            (@qty * (CASE WHEN (detail.Sq_Meter < ISNULL(detail.Min_Charge_Sq_Meter,0)) THEN ISNULL(detail.Min_Charge_Sq_Meter,0) ELSE detail.Sq_Meter END)) + ISNULL(detail.Adjustment,0)
                        ELSE
                            NULL
                    END,
                    CONVERT(nvarchar(50), CONVERT(float, width.Width)) 
                    + 'mm * ' +
                    CASE 
                        WHEN (detail.Overlap IS NOT NULL) THEN
                                '(' + CONVERT(nvarchar(50), CONVERT(float, detail.Length)) + 'mm+' + CONVERT(nvarchar(50), CONVERT(float, detail.Overlap)) + 'mm Overlap/' + CASE WHEN (detail.Overlap_Type = 'Top') THEN 'T' ELSE 'B' END + ')'
                        ELSE
                            CONVERT(nvarchar(50), CONVERT(float, detail.Length)) + 'mm'
                    END
                    + ' * ' +
                    CASE WHEN (detail.Quantity < 10) THEN '0' ELSE '' END
                    +
                    CONVERT(nvarchar(50), CONVERT(float, detail.Quantity))
                    + ' pieces * ' +
                    detail.Panel_Profile,
                    detail.Origin_Service_ID,
                    ISNULL(detail.Unit_Price,0),
                    CASE 
                        WHEN (AS_Profile.Lump_Sum_Surcharge = 'Yes' OR priceTable.Charge_Type = 'Lump Sum') THEN
                            detail.Amount
                        ELSE
                             ((@qty * (CASE WHEN (detail.Sq_Meter < ISNULL(detail.Min_Charge_Sq_Meter,0)) THEN ISNULL(detail.Min_Charge_Sq_Meter,0) ELSE detail.Sq_Meter END)) + ISNULL(detail.Adjustment,0)) * ISNULL(detail.Unit_Price,0)
                    END,
                    detail.AS_Profile_ID
                FROM Service_Confirmation_Detail detail
                INNER JOIN Service_Confirmation ON Service_Confirmation.Service_Confirmation_ID = detail.Service_Confirmation_ID
                INNER JOIN AS_Profile ON AS_Profile.AS_Profile_ID = detail.AS_Profile_ID
                INNER JOIN Width_and_Colour width ON width.Width_and_Colour_ID = detail.Width_and_Colour_ID
                LEFT JOIN
                (
                    SELECT
                        Service_Agreement_ID,
                        AS_Profile_ID,
                        Charge_Type
                    FROM Service_Price
                ) priceTable ON priceTable.AS_Profile_ID = detail.AS_Profile_ID AND priceTable.Service_Agreement_ID = Service_Confirmation.Service_Agreement_ID
                    WHERE detail.Service_Confirmation_Detail_ID = @detailID
                ) ORDER BY detail.Length DESC, width.Width DESC

            SET @invItemID = SCOPE_IDENTITY()
            END
            INSERT INTO Invoice_Detail
            (
                Creation_Time,
                Creator,
                S_N,
                Invoice_ID,
                Invoice_Item_ID,
                Service_Confirmation_Item_ID,
                Origin_Service_ID,
                AS_Profile_ID,
                Description,
                Quantity,
                Sq_Meter,    
                Total_Quantity,
                Charge_Type
            )
            (
                SELECT
                    GETDATE(),
                    '" + UserID + @"',
                    item.S_N,
                    @invoiceID,
                    @invItemID,
                    item.Service_Confirmation_Item_ID,
                    item.Origin_Service_ID,
                    item.AS_Profile_ID,
                    CONVERT(nvarchar(255), item.Width_Description)
                    + ' * ' +
                    CONVERT(nvarchar(255), item.Length_Description)
                    + ' * ' +
                    CASE WHEN (item.Available_Invoice < 10) THEN '0' ELSE '' END
                    +
                    CONVERT(nvarchar(50), CONVERT(float, item.Available_Invoice))
                    + ' pieces * ' +
                    detail.Panel_Profile
                    + ' * ' +
                    item.Panel_Code,
                    item.Available_Invoice,
                    item.Sq_Meter,
                    item.Sq_Meter * item.Available_Invoice,
                    'Services'
                FROM Service_Confirmation_Item item
		        INNER JOIN Service_Confirmation_Detail detail ON detail.Service_Confirmation_Detail_ID = item.Service_Confirmation_Detail_ID
                INNER JOIN Invoice_Item invItem ON invItem.Service_Confirmation_Detail_ID = detail.Service_Confirmation_Detail_ID
                WHERE invItem.Invoice_Item_ID = @invItemID
            ) ORDER BY item.S_N ASC

            ;WITH upd AS
            (
                SELECT
                    detail.S_N,
                    ROW_NUMBER() OVER (PARTITION BY detail.Invoice_Item_ID ORDER BY detail.S_N ASC) AS Seq
                FROM Invoice_Detail detail
                WHERE detail.Invoice_Item_ID = @invItemID
            )
            UPDATE upd
            SET S_N = Seq
        END
	";
}

string result = CommonFunctions.run_SQL_In_Serialized_Transaction(sql);
if (result == "ROLL BACK")
{
    throw new Exception("Add into invoice failed.");
}