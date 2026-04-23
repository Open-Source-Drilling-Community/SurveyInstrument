---
title: "How to use the SurveyInstrument microservice?"
output: html_document
---

Typical Usage
===
1. Upload a new SurveyInstrument using the `Post` web api method.
2. Call the `Get` method with the identifier of the uploaded SurveyInstrument as argument. 
The return Json object contains the SurveyInstrument description.
3. Optionally send a `Delete` request with the identifier of the SurveyInstrument in order to delete the SurveyInstrument if you do not 
want to keep the SurveyInstrument uploaded on the microservice.


