---
name: resx-translation
description: Procedure for generating translations for DAX Studio resource files. Use when creating or updating translations for target languages (es, fr, de, zh-Hans, ja).
---

# Resx Translation Generation

## Overview

Generate translations for DAX Studio's `.resx` resource files. Target languages:
- **es** — Spanish
- **fr** — French
- **de** — German
- **zh-Hans** — Chinese (Simplified)
- **ja** — Japanese

## Process

1. Read the English base `.resx` file (e.g., `Strings.resx`)
2. For each `<data>` entry, translate the `<value>` element
3. Write the translated entries to the language-specific file (e.g., `Strings.es.resx`)

## Translation Rules

### Do NOT Translate
- **DAX function names**: CALCULATE, SUMMARIZE, EVALUATE, MEASURE, VAR, RETURN, DEFINE, ORDER BY, etc.
- **Technical terms that are industry-standard in English**: VertiPaq, Storage Engine, Formula Engine, DirectQuery, xmSQL, ADOMD, XMLA, SSAS, TOM, TMSL
- **Product names**: DAX Studio, Power BI, Power Pivot, Analysis Services, Excel, Azure, SQL Server
- **Format placeholders**: `{0}`, `{1}`, `{2}` — preserve these exactly
- **Keyboard shortcuts**: Ctrl+C, F5, Ctrl+Shift+Enter — keep as-is
- **File extensions**: .csv, .xlsx, .json, .parquet, .vpax

### Translation Guidelines
- Keep translations concise — German and Japanese tend to be longer than English; keep labels short for UI layout
- Preserve any special characters (quotes, parentheses, brackets)
- Maintain the same tone (professional, technical)
- Use formal register (e.g., "usted" in Spanish, "vous" in French, "Sie" in German)
- For Chinese, use Simplified Chinese (zh-Hans), not Traditional
- For Japanese, use standard technical Japanese with appropriate katakana for loanwords

## Resx File Format

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" msdata:Ordinal="0" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute name="xml:space" use="optional" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>

  <data name="ResourceKey" xml:space="preserve">
    <value>Translated text here</value>
  </data>
</root>
```

## Example Translations

| Key | English | Spanish | French | German | Chinese | Japanese |
|-----|---------|---------|--------|--------|---------|----------|
| Ribbon_RunQuery | Run Query | Ejecutar consulta | Exécuter la requête | Abfrage ausführen | 运行查询 | クエリの実行 |
| Status_Connected | Connected | Conectado | Connecté | Verbunden | 已连接 | 接続済み |
| Error_Connection_Timeout | Connection timed out | Tiempo de conexión agotado | Délai de connexion dépassé | Verbindungszeitüberschreitung | 连接超时 | 接続がタイムアウトしました |
