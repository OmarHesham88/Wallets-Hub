"use client";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { AlertCircle, CheckCircle2, CircleDollarSign, Clock3, Smartphone, WalletCards } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, money, User } from "@/lib/api";

type Receipt = { id:string; walletName:string; provider:string; amount:number; currencyCode:string; sender?:string; status:"Pending"|"Confirmed"|"Rejected"; receivedAtUtc:string };
type Wallet = { id:string; name:string; provider:string; currencyCode:string; isActive:boolean };

export default function DashboardPage() {
  const me = useQuery({ queryKey:["me"], queryFn:()=>api<User>("/api/auth/me") });
  const receipts = useQuery({ queryKey:["receipts","dashboard"], queryFn:()=>api<Receipt[]>("/api/receipts"), refetchInterval:15_000 });
  const wallets = useQuery({ queryKey:["wallets"], queryFn:()=>api<Wallet[]>("/api/wallets") });
  const rows=receipts.data??[]; const today=new Date().toDateString(); const todayRows=rows.filter(x=>new Date(x.receivedAtUtc).toDateString()===today); const pending=rows.filter(x=>x.status==="Pending"); const confirmed=todayRows.filter(x=>x.status==="Confirmed");
  const egp=confirmed.filter(x=>x.currencyCode==="EGP").reduce((s,x)=>s+x.amount,0); const usd=confirmed.filter(x=>x.currencyCode==="USD").reduce((s,x)=>s+x.amount,0);
  return <Shell><div className="page-head"><div><span className="eyebrow">Live operations</span><h1>Good day, {me.data?.displayName?.split(" ")[0] ?? "there"}</h1><p>Monitor incoming wallet payments and keep the review queue moving.</p></div><Link className="btn" href="/receipts"><CircleDollarSign size={18}/>Review receipts</Link></div>
    <div className="stats"><div className="stat"><Clock3/><span>Pending review</span><strong>{pending.length}</strong></div><div className="stat"><CheckCircle2/><span>Confirmed today · EGP</span><strong>{money(egp,"EGP")}</strong></div><div className="stat"><CircleDollarSign/><span>Confirmed today · USD</span><strong>{money(usd,"USD")}</strong></div><div className="stat"><WalletCards/><span>Accessible wallets</span><strong>{(wallets.data??[]).filter(x=>x.isActive).length}</strong></div></div>
    <div className="grid two"><section className="panel"><div className="card-top"><div><span className="eyebrow">Review queue</span><h2>Latest pending receipts</h2></div><AlertCircle className="muted"/></div>{pending.slice(0,5).map(row=><Link href="/receipts" className="card-top" style={{padding:"12px 0",borderBottom:"1px solid var(--line)"}} key={row.id}><div><strong>{money(row.amount,row.currencyCode)}</strong><p>{row.walletName} · {row.provider}</p></div><span className="badge">Pending</span></Link>)}{pending.length===0&&<div className="empty" style={{minHeight:170}}><div><CheckCircle2/><h2>Queue is clear</h2><p className="muted">New receipts will appear automatically.</p></div></div>}</section>
    <section className="panel"><div className="card-top"><div><span className="eyebrow">Wallet health</span><h2>Connected coverage</h2></div><Smartphone className="muted"/></div>{(wallets.data??[]).slice(0,6).map(wallet=><div className="card-top" style={{padding:"12px 0",borderBottom:"1px solid var(--line)"}} key={wallet.id}><div><strong>{wallet.name}</strong><p className="muted">{wallet.provider} · {wallet.currencyCode}</p></div><span className={`badge ${wallet.isActive?"success":"danger"}`}>{wallet.isActive?"Active":"Paused"}</span></div>)}</section></div>
  </Shell>;
}
