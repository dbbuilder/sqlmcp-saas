'use client'

import React from 'react'
import Link from 'next/link'
import { ArrowRight, Database, Lock, Zap, Users, BarChart3, Check, Menu, X } from 'lucide-react'

const pricingTiers = [
  {
    name: 'Free',
    price: '$0',
    period: 'forever',
    features: [
      '1 SQL connection',
      '100 queries/month',
      'Basic schema reading',
      'Community support',
      'Basic documentation'
    ],
    cta: 'Start Free',
    featured: false
  },
  {
    name: 'Starter',
    price: '$49',
    period: '/month',
    features: [
      '3 SQL connections',
      '1,000 queries/month',
      'Full schema exploration',
      'Email support',
      'API access',
      'Usage dashboard'
    ],
    cta: 'Start Trial',
    featured: false
  },
  {
    name: 'Pro',
    price: '$199',
    period: '/month',
    features: [
      '10 SQL connections',
      '10,000 queries/month',
      'Advanced Query & Analysis',
      'Priority support',
      'Usage analytics',
      'Team collaboration',
      'Custom integrations'
    ],
    cta: 'Start Trial',
    featured: true
  },
  {
    name: 'Enterprise',
    price: 'Custom',
    period: '',
    features: [
      'Unlimited connections',
      'Custom query limits',
      'All features + Bridge',
      'Dedicated support',
      'Consulting hours included',
      'Custom deployment',
      'SLA guarantee'
    ],
    cta: 'Contact Sales',
    featured: false
  }
]

export default function LandingPage() {
  const [mobileMenuOpen, setMobileMenuOpen] = React.useState(false)

  return (
    <div className="min-h-screen bg-white">
      {/* Navigation */}
      <nav className="fixed top-0 w-full bg-white/80 backdrop-blur-md z-50 border-b border-gray-100">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16 items-center">
            <div className="flex items-center">
              <Link href="/" className="text-2xl font-bold text-blue-600">
                SQLMCP<span className="text-gray-400">.net</span>
              </Link>
            </div>
            
            <div className="hidden md:flex items-center space-x-8">
              <Link href="#features" className="text-gray-600 hover:text-gray-900">Features</Link>
              <Link href="#pricing" className="text-gray-600 hover:text-gray-900">Pricing</Link>
              <Link href="#how-it-works" className="text-gray-600 hover:text-gray-900">How it Works</Link>
              <Link href="/docs" className="text-gray-600 hover:text-gray-900">Docs</Link>
              <Link href="/login" className="text-gray-600 hover:text-gray-900">Login</Link>
              <Link href="/signup" className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 transition">
                Get Started
              </Link>
            </div>

            <div className="md:hidden">
              <button onClick={() => setMobileMenuOpen(!mobileMenuOpen)}>
                {mobileMenuOpen ? <X /> : <Menu />}
              </button>
            </div>
          </div>
        </div>

        {/* Mobile menu */}
        {mobileMenuOpen && (
          <div className="md:hidden bg-white border-b">
            <div className="px-2 pt-2 pb-3 space-y-1">
              <Link href="#features" className="block px-3 py-2 text-gray-600 hover:text-gray-900">Features</Link>
              <Link href="#pricing" className="block px-3 py-2 text-gray-600 hover:text-gray-900">Pricing</Link>
              <Link href="#how-it-works" className="block px-3 py-2 text-gray-600 hover:text-gray-900">How it Works</Link>
              <Link href="/docs" className="block px-3 py-2 text-gray-600 hover:text-gray-900">Docs</Link>
              <Link href="/login" className="block px-3 py-2 text-gray-600 hover:text-gray-900">Login</Link>
              <Link href="/signup" className="block px-3 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
                Get Started
              </Link>
            </div>
          </div>
        )}
      </nav>

      {/* Hero Section */}
      <section className="pt-32 pb-20 px-4 sm:px-6 lg:px-8 bg-gradient-to-b from-blue-50 to-white">
        <div className="max-w-7xl mx-auto text-center">
          <h1 className="text-5xl md:text-6xl font-bold text-gray-900 mb-6">
            Connect Your SQL Database to
            <span className="bg-gradient-to-r from-blue-600 to-emerald-500 bg-clip-text text-transparent"> Any AI Model</span>
          </h1>
          <p className="text-xl text-gray-600 mb-8 max-w-3xl mx-auto">
            SQLMCP.net makes it simple to connect your SQL Server to Claude, ChatGPT, and other AI models using the Model Context Protocol. Start with our free tier and scale as you grow.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link href="/signup" className="bg-blue-600 text-white px-8 py-4 rounded-lg hover:bg-blue-700 transition flex items-center justify-center">
              Start Free Trial <ArrowRight className="ml-2 h-5 w-5" />
            </Link>
            <Link href="#demo" className="bg-white text-gray-700 px-8 py-4 rounded-lg border border-gray-300 hover:border-gray-400 transition">
              Watch Demo
            </Link>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section id="features" className="py-20 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-4xl font-bold text-gray-900 mb-4">Start Simple, Scale Powerfully</h2>
            <p className="text-xl text-gray-600">Begin with basic connectivity and upgrade to advanced features as your needs grow</p>
          </div>

          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-8">
            <div className="bg-white p-8 rounded-xl shadow-lg border border-gray-100">
              <Database className="h-12 w-12 text-blue-600 mb-4" />
              <h3 className="text-xl font-semibold mb-3">Instant SQL Connectivity</h3>
              <p className="text-gray-600">Connect your SQL Server in minutes. Our MCP server automatically discovers your schema and makes it available to AI models.</p>
            </div>

            <div className="bg-white p-8 rounded-xl shadow-lg border border-gray-100">
              <Lock className="h-12 w-12 text-blue-600 mb-4" />
              <h3 className="text-xl font-semibold mb-3">Enterprise Security</h3>
              <p className="text-gray-600">Role-based access control, audit logging, and encrypted connections ensure your data stays secure.</p>
            </div>

            <div className="bg-white p-8 rounded-xl shadow-lg border border-gray-100">
              <Zap className="h-12 w-12 text-blue-600 mb-4" />
              <h3 className="text-xl font-semibold mb-3">Natural Language Queries</h3>
              <p className="text-gray-600">Let AI models query your database using natural language. No SQL knowledge required.</p>
            </div>

            <div className="bg-white p-8 rounded-xl shadow-lg border border-gray-100">
              <BarChart3 className="h-12 w-12 text-blue-600 mb-4" />
              <h3 className="text-xl font-semibold mb-3">Advanced Analytics</h3>
              <p className="text-gray-600">Upgrade to Pro for AI-powered analytics, complex queries, and data insights.</p>
            </div>

            <div className="bg-white p-8 rounded-xl shadow-lg border border-gray-100">
              <Users className="h-12 w-12 text-blue-600 mb-4" />
              <h3 className="text-xl font-semibold mb-3">Expert Support</h3>
              <p className="text-gray-600">Get help when you need it with our coaching, consulting, and DBA services.</p>
            </div>

            <div className="bg-white p-8 rounded-xl shadow-lg border border-gray-100">
              <ArrowRight className="h-12 w-12 text-blue-600 mb-4" />
              <h3 className="text-xl font-semibold mb-3">Seamless Upgrades</h3>
              <p className="text-gray-600">Start free and upgrade anytime. Your connections and configurations carry over automatically.</p>
            </div>
          </div>
        </div>
      </section>

      {/* How It Works */}
      <section id="how-it-works" className="py-20 px-4 sm:px-6 lg:px-8 bg-gray-50">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-4xl font-bold text-gray-900 mb-4">Get Started in 3 Simple Steps</h2>
            <p className="text-xl text-gray-600">From signup to AI-powered queries in minutes</p>
          </div>

          <div className="grid md:grid-cols-3 gap-8">
            <div className="text-center">
              <div className="bg-blue-600 text-white w-16 h-16 rounded-full flex items-center justify-center text-2xl font-bold mx-auto mb-4">
                1
              </div>
              <h3 className="text-xl font-semibold mb-3">Sign Up & Connect</h3>
              <p className="text-gray-600">Create your account and connect your SQL Server using our secure connection wizard.</p>
            </div>

            <div className="text-center">
              <div className="bg-blue-600 text-white w-16 h-16 rounded-full flex items-center justify-center text-2xl font-bold mx-auto mb-4">
                2
              </div>
              <h3 className="text-xl font-semibold mb-3">Configure Access</h3>
              <p className="text-gray-600">Set permissions and choose which tables and views to expose to AI models.</p>
            </div>

            <div className="text-center">
              <div className="bg-blue-600 text-white w-16 h-16 rounded-full flex items-center justify-center text-2xl font-bold mx-auto mb-4">
                3
              </div>
              <h3 className="text-xl font-semibold mb-3">Start Querying</h3>
              <p className="text-gray-600">Use your MCP endpoint with Claude, ChatGPT, or any MCP-compatible AI model.</p>
            </div>
          </div>
        </div>
      </section>

      {/* Pricing Section */}
      <section id="pricing" className="py-20 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-4xl font-bold text-gray-900 mb-4">Simple, Transparent Pricing</h2>
            <p className="text-xl text-gray-600">Start free, upgrade when you need more</p>
          </div>

          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-8">
            {pricingTiers.map((tier) => (
              <div
                key={tier.name}
                className={`bg-white rounded-xl p-8 ${
                  tier.featured
                    ? 'ring-2 ring-blue-600 shadow-xl scale-105'
                    : 'border border-gray-200 shadow-lg'
                }`}
              >
                {tier.featured && (
                  <div className="bg-blue-600 text-white text-sm font-semibold px-3 py-1 rounded-full inline-block mb-4">
                    Most Popular
                  </div>
                )}
                <h3 className="text-2xl font-bold mb-2">{tier.name}</h3>
                <div className="mb-6">
                  <span className="text-4xl font-bold">{tier.price}</span>
                  <span className="text-gray-600">{tier.period}</span>
                </div>
                <ul className="space-y-3 mb-8">
                  {tier.features.map((feature, index) => (
                    <li key={index} className="flex items-start">
                      <Check className="h-5 w-5 text-green-500 mr-2 flex-shrink-0 mt-0.5" />
                      <span className="text-gray-600 text-sm">{feature}</span>
                    </li>
                  ))}
                </ul>
                <Link
                  href={tier.name === 'Enterprise' ? '/contact' : '/signup'}
                  className={`block text-center py-3 px-6 rounded-lg font-semibold transition ${
                    tier.featured
                      ? 'bg-blue-600 text-white hover:bg-blue-700'
                      : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                  }`}
                >
                  {tier.cta}
                </Link>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-20 px-4 sm:px-6 lg:px-8 bg-blue-600 text-white">
        <div className="max-w-4xl mx-auto text-center">
          <h2 className="text-4xl font-bold mb-4">Ready to Connect Your Data to AI?</h2>
          <p className="text-xl mb-8 text-blue-100">
            Join thousands of companies using SQLMCP.net to unlock the power of their SQL data with AI
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link href="/signup" className="bg-white text-blue-600 px-8 py-4 rounded-lg hover:bg-gray-100 transition font-semibold">
              Start Your Free Trial
            </Link>
            <Link href="/contact" className="bg-blue-700 text-white px-8 py-4 rounded-lg hover:bg-blue-800 transition font-semibold">
              Talk to Sales
            </Link>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="bg-gray-900 text-gray-400 py-12 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto grid md:grid-cols-4 gap-8">
          <div>
            <h3 className="text-white font-semibold mb-4">Product</h3>
            <ul className="space-y-2">
              <li><Link href="/features" className="hover:text-white">Features</Link></li>
              <li><Link href="/pricing" className="hover:text-white">Pricing</Link></li>
              <li><Link href="/docs" className="hover:text-white">Documentation</Link></li>
              <li><Link href="/api" className="hover:text-white">API Reference</Link></li>
            </ul>
          </div>
          <div>
            <h3 className="text-white font-semibold mb-4">Company</h3>
            <ul className="space-y-2">
              <li><Link href="/about" className="hover:text-white">About</Link></li>
              <li><Link href="/blog" className="hover:text-white">Blog</Link></li>
              <li><Link href="/careers" className="hover:text-white">Careers</Link></li>
              <li><Link href="/contact" className="hover:text-white">Contact</Link></li>
            </ul>
          </div>
          <div>
            <h3 className="text-white font-semibold mb-4">Services</h3>
            <ul className="space-y-2">
              <li><Link href="/consulting" className="hover:text-white">Consulting</Link></li>
              <li><Link href="/coaching" className="hover:text-white">Coaching</Link></li>
              <li><Link href="/dba-services" className="hover:text-white">DBA Services</Link></li>
              <li><Link href="/support" className="hover:text-white">Support</Link></li>
            </ul>
          </div>
          <div>
            <h3 className="text-white font-semibold mb-4">Legal</h3>
            <ul className="space-y-2">
              <li><Link href="/privacy" className="hover:text-white">Privacy Policy</Link></li>
              <li><Link href="/terms" className="hover:text-white">Terms of Service</Link></li>
              <li><Link href="/security" className="hover:text-white">Security</Link></li>
              <li><Link href="/compliance" className="hover:text-white">Compliance</Link></li>
            </ul>
          </div>
        </div>
        <div className="mt-8 pt-8 border-t border-gray-800 text-center">
          <p>&copy; 2025 SQLMCP.net. All rights reserved.</p>
        </div>
      </footer>
    </div>
  )
}