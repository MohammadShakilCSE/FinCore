import type { PropsWithChildren } from 'react';

function Brand() {
  return <a className="brand" href="/" aria-label="FinCore home"><span className="brand-mark" aria-hidden="true">f<span>•</span></span>FinCore<span className="brand-dot">.</span></a>;
}

export default function AuthLayout({ children }: PropsWithChildren) {
  return <main className="layout">
    <aside className="story">
      <Brand />
      <div className="story-content">
        <div className="eyebrow"><span /> YOUR MONEY. YOUR MOMENTUM.</div>
        <h1>A little clarity.<br />A lot of possibility.</h1>
        <p>A simpler place to manage your money.<br />Built around you, for whatever comes next.</p>
        <div className="illustration" aria-hidden="true">
          <div className="orbit orbit-one" /><div className="orbit orbit-two" />
          <div className="back-card"><span>Small steps. Bigger plans.</span><div className="bars"><i /><i /><i /><i /><i /><i /><i /></div></div>
          <div className="money-card"><div className="card-top"><span>FinCore.</span><span>↗</span></div><span className="card-label">Made for your everyday</span><div className="card-number">•••• &nbsp; •••• &nbsp; •••• &nbsp; 2048</div><div className="card-bottom"><span>ROOM TO GROW</span><span className="card-circles">◯◯</span></div></div>
          <div className="floating-note"><span>↗</span> Move forward with confidence</div>
        </div>
      </div>
      <div className="story-footer"><span className="small-shield">◇</span> Thoughtfully built. Simply FinCore.</div>
    </aside>
    <section className="entry">
      <header className="entry-header"><span>YOUR EVERYDAY FINANCE, SIMPLIFIED</span><span className="header-icon" aria-hidden="true">✳</span></header>
      <div className="form-wrap">
        {children}
      </div>
      <footer className="entry-footer"><span>© {new Date().getFullYear()} FinCore</span><span>A clearer way forward <span aria-hidden="true">↗</span></span></footer>
    </section>
  </main>;
}
