Diese Solutions sind Teil meiner Arbeit als Softwareentwicklerin bei CTO Software GmbH. Mit diesen Tutorials wird das Ziel verfolgt, Entwickler/innen in diesem Unternehmen in die von mir vorangetriebene Weiter- bzw. Neuentwicklung einzuführen und darüber hinaus anzuleiten. Dieses Tutorial ist work-in-progress (Nov. 2022).

Der Quellcode dieses Repository enthält den EventBus aus https://github.com/dotnet-architecture/eShopOnContainers, so dass einfacher verstanden werden kann, wie dieser funktioniert. Integriert ist die in diesem Beispiel verwendete Methode zur Nutzung einer Outbox für mehr Resilienz in der Kommunikation zwischen Services. Infos zum Outbox-Pattern gibt es hier: https://microservices.io/patterns/data/transactional-outbox.html 

Dieses Projekt wurde von mir ursprünglich als Teil des Prototypings während meiner Arbeit erstellt.

Anmerkungen:
- Docker ist Voraussetzung, um diese Solution zum Laufen zu bekommen.
- Zum Aufräumen des Codes nutze ich die VS Erweiterung "CodeMaid" (https://www.codemaid.net/documentation/). Diese hat im Moment leider die Eigenschaft, die Tabs zu verändern, so dass leider nicht alle .cs Dateien gleich formatiert sind.
- Die Datenbank kann über Program.cs über eine äußerst gruselige Methode beim ersten Mal "Run" erzeugt werden (siehe entsprechenden Kommentar im Code) oder man nutzt die beigelegten sample.sql Dateien über das SQL Server Management Studio. Verbindungsdaten finden sich in den appsettings.json.
- Die Codequalität entspricht nicht der "Reinschrift".
