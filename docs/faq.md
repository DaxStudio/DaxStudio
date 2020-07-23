---
title: Frequently Asked Questions
layout: page
---

**Q:** Why do I get out of memory errors when exporting large tables?

**A:** This is due to a limitation of the Microsoft tabular engine. Currently it must materialize the entire uncompressed result set in memory prior to sending it to the client. If you check in Task Manager while running your export you should find that DAX Studio uses a constant amount of memory and rarely uses more than a few hundred MB.

___

**Q:** What License applies to DAX Studio?

**A:** The full License for DAX Studio can be found [here](/documentation/license) it is also available in rtf format in the root of our [Github repository](https://github.com/DaxStudio/DaxStudio)