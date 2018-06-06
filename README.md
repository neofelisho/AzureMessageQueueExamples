# AzureMessageQueueExamples

`Problem:` 這個專案是用來測試在Azure上可行的MessageQueue方案。原本負責開發此項目的同事使用了Redis Pub/Sub來處理這些message，但上到Azure以後，因為App Service scale out的性質，訂閱者變成原本的10倍，同樣的事情就被重覆處理了10次。

`Solution:`
- 使用 Redis SortedSet 來土炮一個: 
    - 優點: 改起來可能比較快。
    - 缺點: 沒有 MQ failover 的機制，如果 process 跑到一半掛了就會漏訊息。

- 使用 Azure Blob Queue:
    - 優點: 實作 trigger 這邊非常簡單。
    - 缺點: 比起前者，在發佈訊息端要改的程式較多。效能較 Service Bus 差，但在發佈訊息端要改的程式量差不多。

- 使用 Azure Service Bus:
    - 優點: 實作 trigger 比 Blob Queue 麻煩一點點，但效能較好。
    - 缺點: 比起 Redis 要改的程式量較大。

`Test:`
- AzureMessageQueueExamples:
    - 這是一個 WebJob 專案，發佈至 App Service 後，將 App Service Plan scale out 至複數個。
    - 這個專案使用了 log4slack，將 QueueProcessor 接收到的 message 送至指定的 slack channel，用以確認是否有類似 Redis Pub/Sub 重覆處理相同訊息的問題。

- MessageSender:
    - 這是一個簡單的 Console 程式，用來將輸入的字串當做 message 送至各個 message queue。