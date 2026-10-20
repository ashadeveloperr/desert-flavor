#!/usr/bin/env python3
"""Standalone, dependency-free deterministic balance harness for Desert Settlement v0.8.
A scripted one-well, three-canal-edge serial intake graph is used for economy tests.
This is NOT a full HexTile/CanalVertex general network solver; the target Unity solver
must be tested separately for branching and shared edges.
Run: python desert_balance_sim_v08.py
"""
from __future__ import annotations
from dataclasses import dataclass
from math import sqrt
from typing import Dict, List, Tuple
import json, csv, sys, random, statistics

LOSS_HALF = sqrt(0.95)

LEVELS: dict[str, list[dict[str, float]]] = {
 'well':    [dict(stone=8, hours=10, water_supply=100, food_use=.25, upkeep=.60), dict(stone=18,hours=10, water_supply=140, food_use=.30,upkeep=.90),dict(stone=30,hours=14,water_supply=190,food_use=.40,upkeep=1.25)],
 'farm':    [dict(stone=12,hours=6,  water_need=30, yield_=4, food_use=.75,upkeep=.50),dict(stone=16,hours=8,water_need=35,yield_=6,food_use=.95,upkeep=.75),dict(stone=26,hours=12,water_need=40,yield_=8.5,food_use=1.20,upkeep=1.10)],
 'quarry':  [dict(stone=14,hours=8,  water_need=25,yield_=1,food_use=.90,upkeep=.70),dict(stone=18,hours=9,water_need=30,yield_=1.6,food_use=1.10,upkeep=.95),dict(stone=28,hours=13,water_need=35,yield_=2.4,food_use=1.35,upkeep=1.30)],
 'mine':    [dict(stone=24,hours=12, water_need=50,yield_=12,food_use=1.80,upkeep=1.50),dict(stone=28,hours=14,water_need=50,yield_=18,food_use=2.20,upkeep=2.00),dict(stone=44,hours=18,water_need=55,yield_=25,food_use=2.80,upkeep=2.90)],
 'auction': [dict(stone=18,hours=12,food_use=.20,upkeep=.60),dict(stone=20,hours=12,food_use=.25,upkeep=.80)],
 'warehouse':[dict(stone=8,hours=6,food_use=0,upkeep=.20,capacity=100),dict(stone=12,hours=8,food_use=0,upkeep=.35,capacity=200)],
 'tank':    [dict(stone=10,hours=6,food_use=0,upkeep=.30,capacity=240,charge=30,discharge=30),dict(stone=14,hours=8,food_use=0,upkeep=.50,capacity=420,charge=45,discharge=45)],
 'canal':   [dict(stone=2,hours=2,food_use=0,upkeep=.05,capacity=100),dict(stone=6,hours=3,food_use=0,upkeep=.08,capacity=150)],
 'gate':    [dict(stone=3,hours=2,food_use=0,upkeep=.03)]
}

@dataclass
class Building:
    kind:str
    level:int=1
    state:str='operating'  # operating / constructing / upgrading / closed
    deadline:int|None=None
    target:int=1

@dataclass
class Job:
    kind:str
    action:str
    request:int
    index:int=1

class Simulation:
    def __init__(self, scenario:str='base', initial_cash:float=1000, initial_stone:float=75,
                 initial_food:float=48, initial_food_coeff:float=1, terrain_mine:float=1,
                 base_flow_factors:Tuple[float,float,float]=(1,1,1), actions=None,
                 purchase_food:dict[int,int]|None=None, purchase_stone:dict[int,int]|None=None, equip_mining:float=1):
        self.scenario=scenario
        self.cash=initial_cash;self.stone=initial_stone;self.food=initial_food;self.F=initial_food_coeff
        self.terrain_mine=terrain_mine;self.flow_factors=base_flow_factors
        self.equip_mining=equip_mining
        self.structures:dict[str,Building]={};self.queues:list[Job]=[]
        self.events:dict[int,list[Job]]={};self.purchase_food=purchase_food or {};self.purchase_stone=purchase_stone or {}
        self.completed_hour:dict[str,int]={};self.bought_food=0;self.paid_food=0;self.bought_stone=0;self.paid_stone=0
        self.log=[];self.overflow_food=0;self.overflow_stone=0;self.shortage=0
        self.first_shortage=None;self.first_auction=None;self.first_insolvency=None
        self.actions=actions or []
        self._plan('well','build',0);self._plan('farm','build',0)
        self._plan('canal1','build',6);self._plan('canal2','build',8)
        self._plan('quarry','build',10);self._plan('mine','build',10)
        self._plan('canal3','build',18);self._plan('auction','build',20)
        for a in self.actions:self._plan(*a)

    @staticmethod
    def kind(job_name:str)->str:
        return 'canal' if job_name.startswith('canal') else job_name
    def _plan(self,name,action,time):
        self.events.setdefault(time,[]).append(Job(name,action,time))
    def data(self,name,lv=None):
        b=self.structures.get(name)
        lvl=lv or (b.level if b else 1)
        return LEVELS[self.kind(name)][lvl-1]
    def maxcap(self):
        cap=100
        b=self.structures.get('warehouse')
        if b and b.state=='operating':cap+=self.data('warehouse').get('capacity',0)
        return cap
    def active(self,name):
        b=self.structures.get(name)
        return bool(b and b.state=='operating')
    def _complete(self,h):
        for name,b in self.structures.items():
            if b.deadline==h:
                if b.state=='upgrading':b.level=b.target
                b.state='operating';b.deadline=None
                self.completed_hour[name]=h
                if name=='auction' and self.first_auction is None:self.first_auction=h+96
    def _start_jobs(self,h):
        currently=sum(b.state in ('constructing','upgrading') for b in self.structures.values())
        # FIFO job queue; if can't fund first job, don't bypass that priority.
        while self.queues and currently<2:
            job=self.queues[0];b=self.structures.get(job.kind)
            if job.action=='build' and b: raise ValueError('duplicate '+job.kind)
            if job.action=='upgrade' and (not b or b.state!='operating'):break
            level=1 if job.action=='build' else b.level+1
            target=self.data(job.kind,lv=level)
            if self.stone +1e-8 < target['stone']:break
            self.queues.pop(0);self.stone-=target['stone']
            if job.action=='build':self.structures[job.kind]=Building(self.kind(job.kind),state='constructing',deadline=h+int(target['hours']))
            else:b.state='upgrading';b.target=level;b.deadline=h+int(target['hours'])
            currently+=1
    def water(self):
        # A single branch: well -> (edge1 midpoint farm) -> (edge2 midpoint quarry) -> (edge3 midpoint mine)
        # Half-edge loss sqrt(.95) applied before/after each midpoint demand.
        out={name:0 for name in ('farm','quarry','mine')}
        if not self.active('well'):return out
        q=self.data('well')['water_supply']
        for i,name in enumerate(('farm','quarry','mine'),1):
            edge='canal'+str(i)
            if not self.active(edge):break
            q=min(q,self.data(edge)['capacity'])
            q*=LOSS_HALF
            if self.active(name):
                need=self.data(name)['water_need'];take=min(need,q)
                out[name]=take/need
                q-=take
            q*=LOSS_HALF
        # Optional terrain factors model farm and quarry unable to receive full water despite
        # target network flow. Do not credit released water to downstream in this stress-test mode.
        return {k:out[k]*self.flow_factors[i] for i,k in enumerate(('farm','quarry','mine'))}
    def buy(self,h):
        amount=self.purchase_food.get(h,0)
        if amount<=0:return
        buy=min(amount,max(0,int(self.maxcap()-self.food)))
        for i in range(buy):
            cost=2*(1.02**self.bought_food)
            if self.cash<cost:break
            self.cash-=cost;self.food+=1;self.bought_food+=1;self.paid_food+=cost
    def buy_stone(self,h):
        requested=self.purchase_stone.get(h,0)
        count=min(requested,max(0,int(self.maxcap()-self.stone)))
        for i in range(count):
            cost=4*(1.03**self.bought_stone)
            if self.cash < cost:break
            self.cash-=cost;self.stone+=1;self.bought_stone+=1;self.paid_stone+=cost
    def tick(self,h):
        self._complete(h)
        for event in self.events.get(h,[]):
            if event.action in ('close','open'):
                b=self.structures.get(event.kind)
                if not b:raise ValueError('Cannot toggle missing '+event.kind)
                if event.action=='close' and b.state=='operating':b.state='closed'
                elif event.action=='open' and b.state=='closed':b.state='operating'
            else:self.queues.append(event)
        self.buy_stone(h)
        self._start_jobs(h)
        self.buy(h)
        water=self.water()
        demand=0;upkeep=0;produced={'food':0.,'stone':0.,'ore':0.}
        for name,b in self.structures.items():
            if b.state=='constructing':continue
            d=self.data(name)
            if b.state=='operating':
                demand+=d.get('food_use',0)
                upkeep+=d.get('upkeep',0)
                if name=='farm':produced['food']+=d.get('yield_',0)*water['farm']*self.F
                if name=='quarry':produced['stone']+=d.get('yield_',0)*water['quarry']*self.F
                if name=='mine':produced['ore']+=d.get('yield_',0)*water['mine']*self.F*self.terrain_mine*self.equip_mining
            else:upkeep+=0.3*d.get('upkeep',0)
        self.cash-=upkeep
        if self.cash<0 and self.first_insolvency is None:self.first_insolvency=h
        raw_food=produced['food'];raw_stone=produced['stone']
        deficit=max(0,demand-(self.food+raw_food))
        unbounded_food=max(0,self.food+raw_food-demand)
        cap=self.maxcap()
        self.overflow_food+=max(0,unbounded_food-cap)
        credited_food=max(0,raw_food-max(0,unbounded_food-cap))
        self.food=min(cap,unbounded_food)
        self.overflow_stone+=max(0,self.stone+raw_stone-cap)
        credited_stone=max(0,raw_stone-max(0,self.stone+raw_stone-cap))
        self.stone=min(cap,self.stone+raw_stone)
        self.shortage+=deficit
        if deficit>1e-9:
            if self.first_shortage is None:self.first_shortage=h
            self.F=max(0,self.F-0.2/24*min(1,deficit/(0.25*demand)))
        elif demand>0 and self.active('farm') and water['farm']>0:
            self.F=min(1,self.F+0.3/24)
        self.log.append(dict(h=h,cash=self.cash,food=self.food,stone=self.stone,F=self.F,
           ore=produced['ore'],foodProd=raw_food,foodValid=credited_food,stoneProd=raw_stone,
           stoneValid=credited_stone,foodDemand=demand,waterFarm=water['farm'],
           waterQuarry=water['quarry'],waterMine=water['mine'],deficit=deficit))
    def run(self,until:int):
        for h in range(until):self.tick(h)
        return self
    def valuation(self,h:int, multiplier:float=1.05):
        q=self.log[max(0,h-24):h]
        if len(q)<24:raise ValueError('not a full 24h window')
        ore=sum(x['ore'] for x in q)
        food=sum(x['foodValid'] for x in q)/24
        stone=sum(x['stoneValid'] for x in q)/24
        mf=1+0.50*min(1,food/8)
        ms=1+1.00*min(1,stone/2)
        infra=1+(0.04 if self.active('well') else 0)+(0.01 if self.active('auction') else 0)+sum(0.01 for i in range(1,4) if self.active('canal'+str(i)))+(0.025 if self.active('warehouse') else 0)
        infra=min(1.20,infra)
        base=4.5*ore
        return dict(h=h,ore=ore,foodAverage=food,stoneAverage=stone,foodMultiplier=mf,stoneMultiplier=ms,infraMultiplier=infra,baseValue=base,value=base*mf*ms*infra,bid=base*mf*ms*infra*multiplier)
    def summary(self,h):
        v=self.valuation(h)
        return dict(scenario=self.scenario,hour=h,auction_start=self.first_auction,
          cash=round(self.cash,2),food=round(self.food,2),stone=round(self.stone,2),
          F=round(self.F,4),foodLost=round(self.overflow_food,2),stoneLost=round(self.overflow_stone,2),
          foodShort=round(self.shortage,3),foodBought=self.bought_food,foodCost=round(self.paid_food,2),stoneBought=self.bought_stone,stoneCost=round(self.paid_stone,2),
          firstShortage=self.first_shortage,insolvencyHour=self.first_insolvency,
          totalStoneValid=round(sum(x['stoneValid'] for x in self.log),2),
          auctionValue=round(v['value'],2),saleBid=round(v['bid'],2),
          cashAfterSale=round(self.cash+v['bid'],2),
          miningPerHour=round(v['ore']/24,2),
          valueDetails={k:round(v[k],3) for k in ('foodAverage','stoneAverage','foodMultiplier','stoneMultiplier','infraMultiplier')},
          completes=dict(self.completed_hour))

def do_tests():
    base=Simulation('base sell at auction1').run(133)
    nochange=Simulation('defer without investment').run(229)
    mineup=Simulation('defer mine upgrade',actions=[('mine','upgrade',133)]).run(229)
    minehouse=Simulation('defer mine upgrade + warehouse',actions=[('mine','upgrade',133),('warehouse','build',133)]).run(229)
    farmup=Simulation('defer farm upgrade',actions=[('farm','upgrade',133)]).run(229)
    quarryup=Simulation('defer quarry upgrade',actions=[('quarry','upgrade',133)]).run(229)
    waterup=Simulation('defer well + canal1 upgrade',actions=[('well','upgrade',133),('canal1','upgrade',133)]).run(229)
    dry=Simulation('dry map, no purchases',base_flow_factors=(.8,1,1)).run(133)
    closeq=Simulation('close quarry last 24h (auction1)',actions=[('quarry','close',109)]).run(133)
    close_end=Simulation('mine upgrade + house, close quarry last24',actions=[('mine','upgrade',133),('warehouse','build',133),('quarry','close',205)]).run(229)
    stonebuy=Simulation('purchase 5 stone at h20 to rush auction',purchase_stone={20:5}).run(128)
    third=Simulation('mine to level3 + warehouse',actions=[('mine','upgrade',133),('warehouse','build',133),('mine','upgrade',148)]).run(229)
    dryhelp=Simulation('dry map, food purchase at h95',base_flow_factors=(.8,1,1),purchase_food={95:30}).run(133)
    for s,h in [(base,133),(nochange,229),(mineup,229),(minehouse,229),(farmup,229),(quarryup,229),(waterup,229),(closeq,133),(close_end,229),(third,229),(stonebuy,128),(dry,133),(dryhelp,133)]:
        print(json.dumps(s.summary(h),ensure_ascii=False))
    assert base.first_auction==133,base.first_auction
    assert base.completed_hour['auction']==37,base.completed_hour
    assert base.stone<=100 and base.food<=100
    assert abs(sum([.25,.75,.9,1.8,.2])-3.9)<1e-8
    assert nochange.summary(229)['saleBid']<base.summary(133)['saleBid']
    assert abs(base.summary(133)['cash']-540.05)<.01
    assert abs(base.water()['mine']-0.742796)<.0001
    assert closeq.summary(133)['cashAfterSale']<base.summary(133)['cashAfterSale']+5
    assert minehouse.summary(229)['saleBid']>mineup.summary(229)['saleBid']
    assert minehouse.F>=0.99
    assert stonebuy.completed_hour['auction']==32 and stonebuy.first_auction==128
    with open('/mnt/data/desert_balance_v08_results.json','w',encoding='utf-8') as f:
        json.dump([s.summary(h) for s,h in [(base,133),(nochange,229),(mineup,229),(minehouse,229),(farmup,229),(quarryup,229),(waterup,229),(closeq,133),(close_end,229),(third,229),(stonebuy,128),(dry,133),(dryhelp,133)]],f,ensure_ascii=False,indent=2)
    with open('/mnt/data/desert_balance_v08_hourly.csv','w',encoding='utf-8',newline='') as f:
        rows=minehouse.log
        w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)

def sensitivity(n=200,seed=42):
    rng=random.Random(seed)
    outcomes=[]
    for k in range(n):
        farm=rng.uniform(.78,1.0)
        quarry=rng.uniform(.88,1.0)
        mine=rng.uniform(.84,1.0)
        ore=rng.uniform(.85,1.15)
        bid=rng.uniform(.90,1.10)
        sim=Simulation('sensitivity-'+str(k),terrain_mine=ore,base_flow_factors=(farm,quarry,mine)).run(133)
        v=sim.valuation(133,multiplier=bid)
        outcomes.append(dict(cashAfterSale=sim.cash+v['bid'],balance=sim.cash,
          f=sim.F,food=sim.food,value=v['value'],bankrupt=sim.first_insolvency is not None))
    arr=[r['cashAfterSale'] for r in outcomes]
    stats=dict(sample_count=n,seed=seed, min=round(min(arr),2),median=round(statistics.median(arr),2),
         max=round(max(arr),2),insolvent=sum(r['bankrupt'] for r in outcomes),
         food_shortage=sum(r['f']<0.99999 for r in outcomes),
         ending_food0=sum(r['food']<0.0001 for r in outcomes))
    with open('/mnt/data/desert_balance_v08_sensitivity.json','w',encoding='utf-8') as f:
        json.dump(stats,f,ensure_ascii=False,indent=2)
    return stats

if __name__=='__main__':
    do_tests()
    print('SENSITIVITY',json.dumps(sensitivity(),ensure_ascii=False))