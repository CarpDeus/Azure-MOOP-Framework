MOOP-Framework-V2
=================
This is a framework designed to implement cloud applications utilizing Microsoft's [Windows Azure](http://windows.azure.com) 
cloud service. The goal was to create a framework that was scalable and flexible and easy to use. Part of this was 
achieved through the use of a combination of HTML pages utilizing JavaScript and generic handlers to handle certain 
types of backend processing such as database access or search requests.  
Rather than serving up pages stored in the VM, the framework serves up objects that are stored in Windows Azure 
Blob Storage. Database queries are defined using XML blocks and interpreted by a handler. Lucene Search queries are 
handled the same way.

#Defining a Database Call#
Database calls are handled by the Queries.ashx handler. The web-config defines this handler so it processes any 
request ending in .q. The handler then looks in a blob container called fragments to find a similarly named xml
blob.
>**Note** File names are case sensitive. *ThisIsADataRequest.q* must match up to *ThisIsADataRequest.xml*.  
The following is an exampe of an XML configuration for a database call:
`<?xml version="1.0"?>
<MOOPData>
<storedProcedure procedureName="" connectionName="" requirePost="false" >
<parameter parameterName="@Clue" urlParameterName="Clue" dataType="nvarchar" dataLength="255" defaultValue="DBNull" isOutput="false" />
<cacheInformation expireSeconds="0" cacheability="private" />
</storedProcedure>
</MOOPData>`