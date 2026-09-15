# Topologia de carga (Fase 6, tarefa 3a)

Caminho completo do `Norn.LoadGenerator` até o Shop, e número de hops — sustenta a ameaça à
validade externa declarada na §2 (o gerador não fala com um serviço em rede real, fala com um
cluster de nó único atrás de NodePort no mesmo host).

## Caminho medido em 15/09/2026

```
Norn.LoadGenerator (host Windows, fora do WSL2 — ADR-18)
  │  HTTP, porta do host
  ▼
localhost:8080 (Catalog) / localhost:8081 (Order)          [hop 1: loopback do host]
  │  k3d mapeia a porta do host para o NodePort do cluster
  │  (`-p "8080:30080@server:0"` / `-p "8081:30081@server:0"`, tarefa 3a)
  ▼
k3d-norn-serverlb:30080/30081 (LoadBalancer do k3d, container Docker)  [hop 2]
  │  proxy TCP do serverlb para o nó
  ▼
k3d-norn-server-0:30080/30081 (kube-proxy, NodePort → Service)        [hop 3]
  │  kube-proxy (iptables/nftables) encaminha para o Pod
  ▼
Pod catalog-api / order-api (porta 8080 do container)                  [hop 4]
```

**4 hops** entre o gerador e o Pod: loopback do host → LoadBalancer do k3d → NodePort do nó →
Pod. Nenhum Ingress, nenhum Traefik (D9, confirmado por `kubectl get pods -A` sem nenhum pod
`traefik*`) — o k3d foi criado com `--k3s-arg "--disable=traefik@server:0"`.

## Payment.API fica fora deste caminho

O `Norn.LoadGenerator` só fala HTTP diretamente com Catalog (`GET /api/v1/products`) e Order
(`POST /api/v1/orders`) — confirmado em `tools/Norn.LoadGenerator/ShopClient.cs`. Payment.API é
acionado somente por eventos RabbitMQ dentro da saga (§5.1), por isso seu `Service` é `ClusterIP`,
sem NodePort.

## Ameaça à validade externa (declarar na monografia, §2)

Todo o caminho — gerador, load balancer do k3d, nó, Pod — roda no mesmo host físico (D7),
compartilhando CPU e a mesma pilha de rede do Windows/WSL2/Docker Desktop. Não há latência de rede
real, não há múltiplos saltos de roteador, e o gerador nunca compete por banda com o tráfego que
está medindo. Os resultados de latência e throughput são válidos **relativos entre os três braços
(A/B/C)**, que correm sob a mesma topologia — não são representativos de uma topologia distribuída
de produção.
